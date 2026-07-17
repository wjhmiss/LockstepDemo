using System;
using System.IO;
using System.Text;
using MemoryPack;

namespace LockstepShared
{
    /// <summary>
    /// 网络层使用的"消息包装"工具类。
    ///
    /// 我们采用“长度前缀 + 消息类型字节 + MemoryPack 二进制”协议：
    ///   [4 字节 int 包体长度，大端序][1 字节消息类型][N 字节 MemoryPack 二进制]
    ///
    /// 协议格式图示：
    ///   +--------+--------+--------+--------+========+========================+
    ///   | 字节0  | 字节1  | 字节2  | 字节3  | 字节4  |     字节5 ... N+4      |
    ///   | 长度高 |        |        | 长度低 | 消息类型|    MemoryPack 二进制   |
    ///   +--------+--------+--------+--------+========+========================+
    ///   |←----------- 4 字节大端序长度 ------------→|← 1 →|←----- N 字节 ----→|
    ///
    /// 其中“包体长度”= 1（消息类型字节）+ N（MemoryPack 二进制长度）。
    ///
    /// 优点：
    ///   - MemoryPack 二进制序列化性能极高，远超 JSON；
    ///   - 框架层通过消息类型字节路由，完全独立于具体类型。
    ///
    /// 为什么需要"长度前缀"？
    ///   TCP 是"流式"协议，发送 100 字节可能被拆成 2 次 50 字节接收，
    ///   也可能 2 次 100 字节被合并成 1 次 200 字节接收（即"粘包"问题）。
    ///   长度前缀让接收方知道"接下来多少字节是一个完整消息"，
    ///   从而正确地从流中切割出每条消息。
    ///
    /// 为什么用大端序 (Big-Endian)？
    ///   大端序是网络字节序的标准约定（高位字节在前），
    ///   保证不同 CPU 架构（x86 是小端序，ARM 可能是大端序）之间的兼容性。
    ///   我们手动处理字节序而非依赖 BitConverter.IsLittleEndian，
    ///   确保代码在所有平台上行为一致。
    /// </summary>
    public static class NetProtocol
    {
        /// <summary>
        /// 将对象序列化为 MemoryPack 二进制并加上 4 字节大端序长度前缀，生成可直接发送的二进制包。
        ///
        /// 打包过程：
        ///   1. 将消息类型字节 + MemoryPack 序列化字节拼接为包体
        ///   2. 将包体长度编码为 4 字节大端序整数（头部）
        ///   3. 拼接头部 + 包体成完整的网络包
        /// </summary>
        /// <param name="obj">要发送的消息对象，必须标注 [MemoryPackable] 属性。</param>
        /// <returns>完整的二进制网络包：[4字节长度头][1字节消息类型][MemoryPack二进制]</returns>
        public static byte[] WriteMessage<T>(T obj)
        {
            // 1) 确定消息类型字节
            byte kind = GetMessageKind(obj);

            // 2) 使用 MemoryPack 序列化为二进制。
            var payload = MemoryPackSerializer.Serialize(obj);

            // 3) 包体 = 1字节kind + payload
            int bodyLen = 1 + payload.Length;
            var packet = new byte[4 + bodyLen];

            // 大端序编码长度头
            packet[0] = (byte)((bodyLen >> 24) & 0xFF);
            packet[1] = (byte)((bodyLen >> 16) & 0xFF);
            packet[2] = (byte)((bodyLen >> 8)  & 0xFF);
            packet[3] = (byte)( bodyLen        & 0xFF);

            // 消息类型字节
            packet[4] = kind;

            // 复制 MemoryPack 二进制载荷
            Array.Copy(payload, 0, packet, 5, payload.Length);
            return packet;
        }

        /// <summary>
        /// 从一个“可累积的读取缓冲”中尝试取出一条完整消息。
        /// 如果长度不足返回 false；如果足够则返回消息类型字节和 MemoryPack 二进制数据并截掉已消费的字节。
        /// 这是流式 TCP 上最稳妥的“粘包处理”写法。
        ///
        /// 处理流程：
        ///   1. 检查缓冲区是否至少有 4 字节（长度前缀）
        ///   2. 读取 4 字节大端序长度，得到消息体长度
        ///   3. 检查缓冲区是否有 4 + len 字节（完整消息）
        ///   4. 如果不完整，返回 false 等待更多数据
        ///   5. 如果完整，提取消息类型字节和 MemoryPack 二进制数据
        /// </summary>
        /// <param name="buffer">累积读取缓冲区。每次收到网络数据就写入此缓冲区。
        /// 使用 MemoryStream 以支持高效地读写和截断。</param>
        /// <param name="kind">输出参数。消息类型字节。</param>
        /// <param name="payload">输出参数。MemoryPack 二进制载荷。</param>
        /// <returns>true 表示成功取出一条消息；false 表示数据不完整，需要等待更多数据。</returns>
        public static bool TryReadMessage(MemoryStream buffer, out byte kind, out byte[] payload)
        {
            kind = 0;
            payload = null;
            var buf = buffer.GetBuffer();
            int total = (int)buffer.Length;
        
            if (total < 4) return false;
        
            int len = (buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3];
        
            if (len < 0 || len > 64 * 1024 * 1024)
            {
                throw new InvalidDataException("NetProtocol packet length out of range: " + len);
            }
        
            if (total < 4 + len) return false;
        
            // 第 5 字节是消息类型
            kind = buf[4];
            // 第 6 字节开始是 MemoryPack 二进制载荷
            int payloadLen = len - 1;
            payload = new byte[payloadLen];
            Array.Copy(buf, 5, payload, 0, payloadLen);
            return true;
        }
        
        /// <summary>
        /// 根据消息对象的运行时类型返回对应的消息类型字节。
        /// </summary>
        private static byte GetMessageKind<T>(T obj)
        {
            switch (obj)
            {
                case HelloMessage:      return MessageType.MsgHello;
                case InputMessage:      return MessageType.MsgInput;
                case GoodbyeMessage:    return MessageType.MsgGoodbye;
                case WelcomeMessage:    return MessageType.MsgWelcome;
                case FrameMessage:      return MessageType.MsgFrame;
                case PlayerJoinMessage: return MessageType.MsgPlayerJoin;
                default:
                    throw new InvalidOperationException($"Unknown message type: {typeof(T).Name}");
            }
        }
    }

    /// <summary>
    /// 消息类型常量（字节）。所有网络消息包都以 1 字节类型标识开头，用于服务端客户端路由分发。
    ///
    /// 消息流向：
    ///   客户端 → 服务端：Hello（加入房间）、Input（上报输入）、Goodbye（退出）
    ///   服务端 → 客户端：Welcome（分配ID）、Frame（广播帧）、PlayerJoin（新玩家通知）、Goodbye（玩家退出）
    /// </summary>
    public static class MessageType
    {
        public const byte MsgHello      = 1;
        public const byte MsgWelcome    = 2;
        public const byte MsgInput      = 3;
        public const byte MsgFrame      = 4;
        public const byte MsgGoodbye    = 5;
        public const byte MsgPlayerJoin = 6;

        // 保留字符串常量兼容层（用于注释和日志）
        public const string Hello      = "hello";
        public const string Welcome   = "welcome";
        public const string Input     = "input";
        public const string Frame     = "frame";
        public const string Goodbye   = "goodbye";
        public const string PlayerJoin = "playerjoin";
    }
}
