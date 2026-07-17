using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MemoryPack;
using LockstepShared;

namespace Lockstep.Client
{
    /// <summary>
    /// 客户端网络通信层。
    /// <para>
    /// 本类封装了与服务端之间的 TCP 长连接，职责非常清晰：
    ///   1. 建立到服务端的 TCP 长连接；
    ///   2. 在后台线程中持续读取服务端发来的数据，按协议解析为完整消息后递交给上层（通过 OnMessage 回调）；
    ///   3. 暴露 Send() 方法供上层发送上行消息。
    /// </para>
    /// <para>
    /// 这一层与游戏业务完全无关，只负责"收发字节流"和"按协议切包"，
    /// 与服务端的 NetProtocol 配对使用（两端使用相同的"4字节长度头 + 1字节消息类型 + MemoryPack二进制"协议）。
    /// </para>
    /// <para>
    /// 网络协议格式（与服务端 NetProtocol 一致）：
    ///   [4字节大端长度头] [1字节消息类型] [MemoryPack 二进制载荷]
    ///   长度头表示后续消息体（类型字节 + 载荷）的总字节数，不包含自身 4 字节。
    /// </para>
    /// </summary>
    public class NetClient
    {
        /// <summary>
        /// TCP 客户端对象，用于建立和服务端的连接。
        /// </summary>
        private TcpClient _tcp;

        /// <summary>
        /// 网络数据流，用于从服务端读取数据和向服务端写入数据。
        /// </summary>
        private NetworkStream _stream;

        /// <summary>
        /// 接收缓冲区：存储从服务端读取但尚未解析为完整消息的原始字节。
        /// <para>
        /// 由于 TCP 是流式协议，一次 Read 可能收到"半个消息"或"一个半消息"，
        /// 因此需要用缓冲区暂存，再由 TryConsumeOneMessage 按协议切包。
        /// </para>
        /// </summary>
        private readonly MemoryStream _recvBuffer = new MemoryStream();

        /// <summary>
        /// 连接存活标志。当连接断开（Read 返回 0 或异常）时设为 false。
        /// 外部可通过此字段判断网络是否仍然可用。
        /// </summary>
        public bool IsAlive;

        /// <summary>
        /// 上层注入的"消息回调"。每从网络中解析出一条完整的消息，就调用一次。
        /// <para>
        /// 参数1：消息类型字节（kind）。
        /// 参数2：MemoryPack 二进制载荷，上层根据 kind 用 MemoryPackSerializer.Deserialize 反序列化。
        /// </para>
        /// </summary>
        public Action<byte, byte[]> OnMessage;

        /// <summary>
        /// 构造函数：建立到服务端的 TCP 连接，并启动后台接收线程。
        /// <para>
        /// 构造完成后连接即建立完毕，如果连接失败会抛出 SocketException。
        /// 后台接收线程自动启动，开始持续监听服务端消息。
        /// </para>
        /// </summary>
        /// <param name="host">服务端 IP 地址，如 "127.0.0.1"</param>
        /// <param name="port">服务端端口号，如 7777</param>
        /// <exception cref="SocketException">连接失败时抛出</exception>
        public NetClient(string host, int port)
        {
            _tcp = new TcpClient();
            Console.WriteLine($"[NetClient] connecting to {host}:{port}...");
            _tcp.Connect(host, port);       // 同步连接，失败则抛异常
            _stream = _tcp.GetStream();     // 获取网络读写流
            IsAlive = true;                 // 标记连接存活

            // 启动后台读循环线程。
            // IsBackground=true 表示该线程为后台线程，主线程退出时自动终止，不会阻止程序退出。
            new Thread(RecvLoop) { IsBackground = true }.Start();
        }

        /// <summary>
        /// 向服务端发送一条消息。
        /// <para>
        /// 使用 MemoryPack 将对象序列化为二进制，再按协议格式（4字节长度头 + 1字节消息类型 + MemoryPack二进制）写入网络流。
        /// </para>
        /// <para>
        /// 注意：此方法是线程安全的。通过 lock(_stream) 保证多线程同时调用 Send 时
        /// 不会出现两次 Write 交叉。
        /// </para>
        /// </summary>
        /// <param name="obj">要发送的消息对象，必须标注 [MemoryPackable] 属性。</param>
        public void Send<T>(T obj)
        {
            // 连接已断开则直接返回，避免异常
            if (!IsAlive) return;

            // 使用 MemoryPack 序列化为二进制
            var payload = MemoryPackSerializer.Serialize(obj);

            // 确定消息类型字节
            byte kind = GetMessageKind(obj);

            // 包体 = 1字节kind + payload
            int bodyLen = 1 + payload.Length;
            var packet = new byte[4 + bodyLen];

            // 4 字节 big-endian 长度前缀
            packet[0] = (byte)((bodyLen >> 24) & 0xFF);
            packet[1] = (byte)((bodyLen >> 16) & 0xFF);
            packet[2] = (byte)((bodyLen >> 8)  & 0xFF);
            packet[3] = (byte)( bodyLen        & 0xFF);

            // 消息类型字节
            packet[4] = kind;

            // 复制 MemoryPack 载荷
            Array.Copy(payload, 0, packet, 5, payload.Length);

            // 加锁保证多线程安全：连续两次 Write 不会被插花
            lock (_stream)
            {
                _stream.Write(packet, 0, packet.Length);
            }
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
                default:
                    throw new InvalidOperationException($"Unknown message type: {typeof(T).Name}");
            }
        }

        /// <summary>
        /// 后台接收循环：持续从网络流中读取数据，解析为完整消息后回调上层。
        /// <para>
        /// 该方法在后台线程中运行，生命周期与连接相同。
        /// 工作流程：
        ///   1. 从 NetworkStream 中读取数据到临时缓冲区；
        ///   2. 将读取到的数据追加到 _recvBuffer（接收缓冲区）；
        ///   3. 循环调用 TryConsumeOneMessage，从缓冲区中按协议格式切出完整消息；
        ///   4. 每切出一条完整消息，调用 OnMessage 回调通知上层。
        /// </para>
        /// <para>
        /// 退出条件：Read 返回 0（服务端正常关闭连接）或抛出异常（网络中断）。
        /// 退出后设置 IsAlive = false。
        /// </para>
        /// </summary>
        private void RecvLoop()
        {
            // 临时读取缓冲区，16KB 大小。每次 Read 最多读取这么多的数据。
            var buf = new byte[16 * 1024];
            try
            {
                while (IsAlive)
                {
                    // 从网络流中读取数据。Read 是阻塞调用，直到有数据可读或连接关闭。
                    // n = 实际读取到的字节数；n=0 表示服务端已关闭连接。
                    int n = _stream.Read(buf, 0, buf.Length);
                    if (n <= 0) break;  // 连接关闭，退出循环

                    // 将新数据追加到接收缓冲区，等待后续解析
                    _recvBuffer.Write(buf, 0, n);

                    // 循环尝试从缓冲区中解析出完整消息。
                    // 一次 Read 可能包含多条消息，所以用 while 循环处理。
                    while (TryConsumeOneMessage(out var kind, out var payload))
                    {
                        // 解析出一条完整消息，回调通知上层处理
                        OnMessage?.Invoke(kind, payload);
                    }
                }
            }
            catch (Exception ex)
            {
                // 网络异常（如连接中断、超时等），打印错误信息
                Console.WriteLine($"[NetClient] recv error: {ex.Message}");
            }
            finally
            {
                // 无论正常退出还是异常退出，都标记连接已断开
                IsAlive = false;
                Console.WriteLine("[NetClient] disconnected.");
            }
        }

        /// <summary>
        /// 尝试从接收缓冲区中解析出一条完整的消息。
        /// <para>
        /// 协议格式：[4字节大端长度头] [1字节消息类型] [MemoryPack 二进制载荷]
        ///   - 长度头：4 个字节，大端序（Big-Endian），表示后续消息体的字节数（含类型字节）
        ///   - 消息类型：1 字节
        ///   - 载荷：MemoryPack 序列化的二进制数据
        /// </para>
        /// </summary>
        /// <param name="kind">输出参数：消息类型字节</param>
        /// <param name="payload">输出参数：MemoryPack 二进制载荷</param>
        /// <returns>true 表示成功解析出一条消息；false 表示缓冲区中没有完整消息</returns>
        /// <exception cref="InvalidDataException">当长度头超出合理范围时抛出</exception>
        private bool TryConsumeOneMessage(out byte kind, out byte[] payload)
        {
            kind = 0;
            payload = null;

            // 读取缓冲区当前的总长度
            long len = _recvBuffer.Length;

            // 如果缓冲区不足 4 字节，连长度头都读不出来，等待更多数据
            if (len < 4) return false;

            // 将缓冲区内容读取到字节数组中以便处理
            _recvBuffer.Position = 0;
            var arrayBuf = new byte[len];
            _recvBuffer.Read(arrayBuf, 0, (int)len);

            // 解析 4 字节长度头（大端序 Big-Endian）
            int packetLen = (arrayBuf[0] << 24) | (arrayBuf[1] << 16) | (arrayBuf[2] << 8) | arrayBuf[3];

            // 安全检查：长度头必须在合理范围内
            if (packetLen < 0 || packetLen > 64 * 1024 * 1024)
                throw new InvalidDataException("client pkt len out of range");

            // 如果缓冲区中的数据还不足以组成一条完整消息，等待更多数据
            if (len < 4 + packetLen) return false;

            // 提取消息类型字节（第5字节）
            kind = arrayBuf[4];

            // 提取 MemoryPack 二进制载荷
            int payloadLen = packetLen - 1;
            payload = new byte[payloadLen];
            Array.Copy(arrayBuf, 5, payload, 0, payloadLen);

            // === 处理缓冲区中剩余的数据 ===
            long remain = len - (4 + packetLen);
            if (remain > 0)
            {
                // 将剩余数据前移到数组开头
                Array.Copy(arrayBuf, 4 + packetLen, arrayBuf, 0, remain);
            }

            // 重建缓冲区：清空后写入剩余数据
            _recvBuffer.SetLength(0);
            _recvBuffer.Write(arrayBuf, 0, (int)remain);

            return true;  // 成功解析出一条消息
        }
    }
}
