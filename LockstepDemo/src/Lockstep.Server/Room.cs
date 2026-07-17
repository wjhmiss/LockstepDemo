using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MemoryPack;
using LockstepShared;

namespace Lockstep.Server
{
    /// <summary>
    /// 客户端连接会话，代表一个与服务端建立 TCP 连接的玩家。
    /// <para>核心职责：</para>
    /// <list type="number">
    ///   <item>在独立的后台线程中持续读取 socket 数据，解析出消息后交给 Room 处理。</item>
    ///   <item>在独立的后台线程中从发送队列取出数据，写入 socket 发给客户端。</item>
    ///   <item>维护接收缓冲区，处理 TCP 粘包/拆包问题。</item>
    /// </list>
    /// <para>
    /// 线程模型：每个 ClientSession 拥有两个后台线程——RecvLoop（接收）和 SendLoop（发送），
    /// 加上 Room 的 tick 线程，共有三个线程可能访问会话数据，因此需要注意线程安全。
    /// </para>
    /// </summary>
    public class ClientSession : IDisposable
    {
        /// <summary>
        /// 会话唯一标识，由 Program.Main 中通过 Interlocked.Increment 分配。
        /// 用于区分不同的连接，即使同一个玩家断线重连也会获得新的 SessionId。
        /// </summary>
        public int SessionId;

        /// <summary>
        /// 玩家 ID，由 Room.AddSession 分配。
        /// 初始值为 -1 表示尚未分配；分配后在整个房间生命周期内唯一标识一个玩家。
        /// 与 SessionId 不同，PlayerId 是游戏逻辑层面的标识。
        /// </summary>
        public int PlayerId { get; set; } = -1;

        /// <summary>
        /// 玩家名称，客户端通过 Hello 消息上报。
        /// </summary>
        public string PlayerName;

        /// <summary>
        /// 底层 TCP 客户端对象，用于网络通信。
        /// </summary>
        public TcpClient Tcp;

        /// <summary>
        /// 会话是否存活。当接收/发送出现错误或远端关闭时置为 false。
        /// 其他模块（如 Room 广播）通过检查此标志决定是否向该会话发消息。
        /// </summary>
        public bool IsAlive = true;

        /// <summary>
        /// 所属房间引用，用于将收到的消息转发给房间处理。
        /// </summary>
        private readonly Room _room;

        /// <summary>
        /// TCP 网络流，用于读写 socket 数据。
        /// </summary>
        private readonly NetworkStream _stream;

        /// <summary>
        /// 接收缓冲区，用于累积从 socket 收到的原始字节。
        /// <para>
        /// 为什么需要缓冲区？因为 TCP 是字节流协议，不保证一次 Read 恰好读到一条完整消息。
        /// 可能出现"粘包"（一次读到多条消息）或"拆包"（一条消息分多次才读完）。
        /// 我们把所有收到的数据先放进缓冲区，再从缓冲区中尝试逐条解析消息。
        /// </para>
        /// </summary>
        private readonly MemoryStream _recvBuffer = new MemoryStream();

        /// <summary>
        /// 发送队列：线程安全的阻塞集合，用于在多线程间安全地传递待发送数据。
        /// <para>
        /// 设计思路：
        /// 所有输出消息先序列化成 byte[] 放入此队列，再由 SendLoop 线程取出并写入 socket。
        /// 这种"生产者-消费者"模式避免了多个线程同时访问 NetworkStream 导致的并发问题，
        /// 是一种简单且实用的线程安全策略。
        /// </para>
        /// <para>
        /// boundedCapacity: 1024 表示队列最多缓存 1024 条待发送消息，
        /// 超过后 Add() 会阻塞，防止内存无限增长。
        /// </para>
        /// </summary>
        private readonly BlockingCollection<byte[]> _sendQueue = new BlockingCollection<byte[]>(boundedCapacity: 1024);

        /// <summary>
        /// 构造函数：初始化会话并立即启动收发线程。
        /// </summary>
        /// <param name="sessionId">会话唯一 ID，由 Program.Main 分配。</param>
        /// <param name="tcp">已建立的 TCP 客户端连接。</param>
        /// <param name="room">所属房间实例，用于转发消息。</param>
        public ClientSession(int sessionId, TcpClient tcp, Room room)
        {
            SessionId = sessionId;
            Tcp = tcp;
            _room = room;
            _stream = tcp.GetStream();

            // 启动接收线程：在后台持续读取 socket 数据
            new Thread(RecvLoop) { IsBackground = true }.Start();
            // 启动发送线程：在后台持续从队列取数据并写入 socket
            new Thread(SendLoop) { IsBackground = true }.Start();
        }

        /// <summary>
        /// 向客户端发送消息。
        /// <para>
        /// 此方法是线程安全的，可以从任意线程调用。
        /// 它只负责将消息序列化后放入发送队列，实际的 socket 写入由 SendLoop 完成。
        /// </para>
        /// </summary>
        /// <param name="obj">要发送的消息对象，会被 NetProtocol.WriteMessage 序列化为二进制包。</param>
        public void Send<T>(T obj)
        {
            // 构完整包，扔进队列由 SendLoop 实际写 socket。
            var packet = NetProtocol.WriteMessage(obj);
            _sendQueue.Add(packet);
        }

        /// <summary>
        /// 接收循环：在后台线程中持续从 socket 读取数据，解析出消息后交给 Room 处理。
        /// <para>
        /// 执行流程：
        /// </para>
        /// <list type="number">
        ///   <item>从 NetworkStream 读取原始字节到临时缓冲区。</item>
        ///   <item>将读到的字节追加到 _recvBuffer（累积缓冲区）。</item>
        ///   <item>循环尝试从 _recvBuffer 中解析出一条完整消息。</item>
        ///   <item>将解析出的消息交给 Room.HandleMessage 处理。</item>
        /// </list>
        /// <para>
        /// 当连接断开（Read 返回 0）或发生异常时，会清理资源并通知房间。
        /// </para>
        /// </summary>
        private void RecvLoop()
        {
            // 临时读取缓冲区，16KB 大小，足够一次读入较多数据
            var buf = new byte[16 * 1024];
            try
            {
                while (IsAlive)
                {
                    // 从 socket 读取数据。Read 是阻塞调用，会等到有数据或连接关闭。
                    int n = _stream.Read(buf, 0, buf.Length);
                    if (n <= 0) break; // 返回 0 表示远端已正常关闭连接

                    // 将本次读到的字节追加到累积缓冲区
                    _recvBuffer.Write(buf, 0, n);

                    // 循环尝试从缓冲区中解析出一条完整消息
                    // 一批数据中可能包含多条消息（粘包），所以用 while 循环全部取出
                    while (TryConsumeOneMessage(out var kind, out var payload))
                    {
                        // 将解析出的消息交给房间处理
                        _room.HandleMessage(this, kind, payload);
                    }
                }
            }
            catch (Exception ex)
            {
                // 读取异常（如连接中断、网络故障等），打印错误信息
                Console.WriteLine($"[Session:{SessionId}] recv error: {ex.Message}");
            }
            finally
            {
                // 无论如何都执行清理工作
                IsAlive = false;                                     // 标记会话已死亡
                _room.NotifySessionLeft(this);                       // 通知房间该玩家已离开
                _sendQueue.CompleteAdding();                          // 通知发送队列不再有新数据，SendLoop 会自然退出
            }
        }

        /// <summary>
        /// 尝试从接收缓冲区中解析出一条完整消息。
        /// <para>
        /// 网络协议格式：[4字节长度][1字节消息类型][MemoryPack 二进制载荷]
        /// - 前4字节是大端序（Big-Endian）的整数，表示消息体的字节长度（含类型字节）。
        /// - 第5字节是消息类型标识。
        /// - 后续是 MemoryPack 序列化的二进制载荷。
        /// </para>
        /// </summary>
        /// <param name="kind">输出参数：消息类型字节。</param>
        /// <param name="payload">输出参数：MemoryPack 二进制载荷。</param>
        /// <returns>是否成功解析出一条完整消息。</returns>
        private bool TryConsumeOneMessage(out byte kind, out byte[] payload)
        {
            kind = 0;
            payload = null;
            // 用 _recvBuffer 整体喂入 TryReadMessage，它在收到完整消息时会截掉已消费字节。
            // 我们把缓冲"转换视角"成 MemoryStream；这里复用同一个。
            // 简单做法：先把缓冲返回到开头，让 TryReadMessage 自始读。
            // 由于 TryReadMessage 不改 buffer 位置，故需手工做截断。
            long len = _recvBuffer.Length;

            // 数据不足4字节，连"消息长度"都读不出来，等待更多数据
            if (len < 4)
                return false;

            // 将整个缓冲区内容读取到数组中以便处理
            _recvBuffer.Position = 0;
            var arrayBuf = new byte[len];
            _recvBuffer.Read(arrayBuf, 0, (int)len);

            // 解析消息长度：前4字节是大端序的32位整数
            // 大端序：高位字节在前，低位字节在后。
            // 例如 [0x00, 0x00, 0x01, 0x2C] 表示长度 = 0*256^3 + 0*256^2 + 1*256 + 44 = 300
            int packetLen = (arrayBuf[0] << 24) | (arrayBuf[1] << 16) | (arrayBuf[2] << 8) | arrayBuf[3];

            // 合法性检查：消息长度不能为负数，也不能超过 64MB（防止恶意包导致内存爆炸）
            if (packetLen < 0 || packetLen > 64 * 1024 * 1024)
                throw new InvalidDataException("server packet len out of range: " + packetLen);

            // 数据不足"4字节头 + 消息体"，说明消息还没收完，等待更多数据
            if (len < 4 + packetLen)
                return false;

            // 返回消息类型字节和 MemoryPack 二进制载荷
            kind = arrayBuf[4];
            int payloadLen = packetLen - 1;
            payload = new byte[payloadLen];
            Array.Copy(arrayBuf, 5, payload, 0, payloadLen);

            // 从缓冲区中删除已消费的字节（4字节头 + packetLen字节消息体），保留剩余数据
            // 剩余数据可能是下一条消息的部分内容
            long remain = len - (4 + packetLen);
            if (remain > 0)
            {
                // 将未消费的字节移到数组头部
                Array.Copy(arrayBuf, 4 + packetLen, arrayBuf, 0, remain);
            }
            // 重置缓冲区并写入剩余数据
            _recvBuffer.SetLength(0);
            _recvBuffer.Write(arrayBuf, 0, (int)remain);
            return true;
        }

        /// <summary>
        /// 发送循环：在后台线程中持续从发送队列取出数据并写入 socket。
        /// <para>
        /// 使用 BlockingCollection 的 GetConsumingEnumerable() 方法，
        /// 它会在队列有数据时返回，队列为空时阻塞等待，队列调用 CompleteAdding 后结束枚举。
        /// 这种方式比手动轮询更高效，不需要 Sleep 或信号量。
        /// </para>
        /// </summary>
        private void SendLoop()
        {
            try
            {
                // GetConsumingEnumerable 是阻塞迭代器：
                // - 队列有数据 → 立即返回一条
                // - 队列为空 → 阻塞等待直到有新数据或 CompleteAdding 被调用
                // - CompleteAdding 后 → 枚举结束，退出循环
                foreach (var packet in _sendQueue.GetConsumingEnumerable())
                {
                    _stream.Write(packet, 0, packet.Length);
                }
            }
            catch (Exception ex)
            {
                // 发送异常（如连接中断），打印错误信息
                Console.WriteLine($"[Session:{SessionId}] send error: {ex.Message}");
            }
            finally
            {
                // 标记会话已死亡，让其他模块不再向此会话发消息
                IsAlive = false;
            }
        }

        /// <summary>
        /// 释放资源：关闭 TCP 连接，标记会话为不存活。
        /// 实现 IDisposable 接口，支持 using 语句自动清理。
        /// </summary>
        public void Dispose()
        {
            IsAlive = false;
            try { Tcp?.Dispose(); } catch { }  // 安全关闭，忽略可能的异常
        }
    }

    /// <summary>
    /// 帧同步房间——服务端逻辑核心。
    /// <para>核心职责：</para>
    /// <list type="number">
    ///   <item>维护当前所有 (PlayerId, Session) 的映射，管理玩家的加入和离开。</item>
    ///   <item>按固定节拍 tick：把这一 tick 内所有玩家上报的输入聚合成 FrameData（帧数据）。</item>
    ///   <item>将 FrameData 广播给所有玩家；同时推进服务端自己的 Simulation（用于引用/对比、可选）。</item>
    /// </list>
    /// <para>
    /// 帧同步服务端最典型的"失误"（新手务必注意）：
    /// </para>
    /// <list type="a">
    ///   <item>收输入要看 tick，不能把上一 tick 的玩家输入错塞进当前帧；</item>
    ///   <item>玩家断开要补空输入，不能让整局卡死；</item>
    ///   <item>同一 tick 内收到的多个输入要"合并"而非"覆盖"（本例只保留最后一个版本，新手可接受）。</item>
    /// </list>
    /// <para>
    /// 关于"补空帧"：
    /// 在帧同步模型中，每一帧必须包含所有玩家的输入，客户端才能推进模拟。
    /// 如果某个玩家某个 tick 没有上报输入（网络延迟或掉线），
    /// 服务端必须为该玩家填充一个"空输入"（如 MoveDir=0, Fire=false），
    /// 否则所有客户端都会卡在那一帧等待，导致整局游戏死锁。
    /// </para>
    /// </summary>
    public class Room
    {
        /// <summary>
        /// 帧计时器，负责根据经过时间计算当前应该推进多少个 tick。
        /// <para>
        /// 初始化时传入 GameConfig.ServerTickMs（每 tick 的毫秒数），
        /// 调用 Update() 时会返回"应该执行的 tick 数"，可能为 0（时间还不够一个 tick）或大于 1（补偿之前落后的帧）。
        /// </para>
        /// </summary>
        private readonly TickTimer _tickTimer = new TickTimer(GameConfig.ServerTickMs);

        /// <summary>
        /// 所有活跃会话字典。键是 SessionId，值是 ClientSession。
        /// <para>
        /// 通过此字典可以遍历所有在线玩家，用于广播帧数据和处理玩家离开。
        /// </para>
        /// </summary>
        private readonly Dictionary<int, ClientSession> _sessions = new Dictionary<int, ClientSession>();

        /// <summary>
        /// PlayerId 分配计数器，从 1 开始递增。
        /// <para>
        /// PlayerId 是游戏逻辑层面的玩家标识，一旦分配就不变（与 SessionId 不同）。
        /// 在帧同步中，PlayerId 用于标识每个玩家的输入。
        /// </para>
        /// </summary>
        private int _nextPlayerId = 1;

        /// <summary>
        /// 当前 tick 号（帧号）。服务端启动后第一次 tick 会赋 1，之后每 tick 递增。
        /// <para>
        /// tick 号是帧同步的核心概念：所有客户端必须按相同的 tick 号、相同的输入序列来驱动模拟，
        /// 才能保证结果一致。
        /// </para>
        /// </summary>
        private int _currentTick = 0;

        /// <summary>
        /// 每个 tick 内收到的所有玩家输入：Dictionary(PlayerId, PlayerInput)。
        /// <para>
        /// 注意并发问题：
        /// - 此字典在 tick 线程（ProduceOneFrame）中被读取和清空；
        /// - 在 RecvLoop 线程（HandleMessage → MessageType.Input）中被写入。
        /// 为维护简单，本示例使用 _inputLock 锁来保护。
        /// </para>
        /// </summary>
        private readonly Dictionary<int, PlayerInput> _pendingInputs = new Dictionary<int, PlayerInput>();

        /// <summary>
        /// 输入锁，保护 _pendingInputs、_sessions、_sim 等共享数据的并发访问。
        /// <para>
        /// 凡是访问上述共享数据的地方，都需要先获取此锁，确保同一时刻只有一个线程在修改。
        /// </para>
        /// </summary>
        private readonly object _inputLock = new object();

        /// <summary>
        /// 服务端自己的 Simulation（模拟）实例和内部的 World（游戏世界）。
        /// <para>
        /// 用途：
        /// </para>
        /// <list type="a">
        ///   <item>给晚加入的玩家发送当前"世界快照"（本示例暂未实现，但架构预留了）；</item>
        ///   <item>调试对比——如果服务端模拟结果与客户端不一致，说明某处逻辑有非确定性问题。</item>
        /// </list>
        /// </summary>
        private readonly Simulation _sim = new Simulation();

        /// <summary>
        /// 服务端 tick 入口方法，由 Program.RoomTickLoop 在后台高频调用。
        /// <para>
        /// 内部由 TickTimer.Update() 计算本周期应该推进几个 tick，
        /// 然后循环调用 ProduceOneFrame() 来逐帧生产。
        /// </para>
        /// <para>
        /// 为什么可能一次推进多个 tick？
        /// 因为 Thread.Sleep(1) 不精确，可能某次醒来时已经过了好几个 tick 的时间。
        /// TickTimer 会累计这些"欠下的" tick，在这里一次性补偿回来。
        /// </para>
        /// </summary>
        public void Tick()
        {
            // Update() 返回本次应该执行的 tick 数（可能为 0、1 或更多）
            int ticks = _tickTimer.Update();
            // 逐帧生产，每帧都会收集输入、推进模拟、广播帧数据
            for (int i = 0; i < ticks; i++)
            {
                ProduceOneFrame();
            }
        }

        /// <summary>
        /// 将新连接的会话加入房间。
        /// <para>
        /// 执行流程：
        /// </para>
        /// <list type="number">
        ///   <item>为该会话分配一个唯一的 PlayerId。</item>
        ///   <item>将 PlayerId 和 Session 的映射加入 _sessions 字典。</item>
        ///   <item>在服务端 Simulation 中添加该玩家。</item>
        ///   <item>向新玩家发送 Welcome 消息，告知其 PlayerId、房间内已有玩家列表、当前服务端 tick。</item>
        ///   <item>向房间内其他玩家广播 PlayerJoin 消息，通知他们有新玩家加入。</item>
        /// </list>
        /// </summary>
        /// <param name="session">新加入的客户端会话。</param>
        public void AddSession(ClientSession session)
        {
            // 分配玩家 Id，加入到 _sessions；
            // 注意房间状态在 tick 线程之外修改，需要同步。
            int newId;
            lock (_inputLock)
            {
                // 分配并自增 PlayerId
                newId = _nextPlayerId++;
                // 将 PlayerId 关联到会话
                session.PlayerId = newId;
                // 将会话加入字典
                _sessions[session.SessionId] = session;
                // 在服务端模拟中添加该玩家
                _sim.AddPlayer(newId);
            }
            Console.WriteLine($"[Server] assigned PlayerId={newId} to session={session.SessionId}");

            // 1) 给新玩家发 Welcome：告知"你是 PlayerId X"，以及房间内**除自己之外**的玩家。
            //    ★ 注意：新会话已经在上面加入 _sessions，这里必须排除自己，否则 ExistingPlayers 会带上
            //      新玩家自己的 Id；客户端又会再 Add(myId) 一次 → 列表变成 [1,1]，导致后续出现重复实体 /
            //      "KeyNotFound"之类的次生 bug。
            var existingPlayers = new List<int>();
            lock (_inputLock)
            {
                foreach (var kv in _sessions)
                {
                    if (kv.Value == session) continue;   // 不把自己算进"已有玩家"
                    existingPlayers.Add(kv.Value.PlayerId);
                }
            }
            session.Send(new WelcomeMessage
            {
                Type = MessageType.Welcome,
                PlayerId = newId,
                ExistingPlayers = existingPlayers,
                ServerTick = _currentTick,
            });

            // 2) 给房内所有已存在玩家发一个"新玩家加入"的 Welcome 风格更新：
            //    我们用 FrameData 里 PlayerInput 的"列表广播"方式即可，这里只单独通知房间人数。
            //    本示例简化：直接将新玩家 PlayerId 加入双方 Simulation。
            lock (_inputLock)
            {
                foreach (var kv in _sessions)
                {
                    if (kv.Value == session) continue;
                    // ★ 通知已有玩家：新玩家加入了，让他们把新玩家 AddPlayer 进 Simulation。
                    kv.Value.Send(new PlayerJoinMessage
                    {
                        Type = MessageType.PlayerJoin,
                        PlayerId = newId,
                    });
                }
            }
        }

        /// <summary>
        /// 通知房间某会话已离开（玩家断线或关闭连接）。
        /// <para>
        /// 执行流程：
        /// </para>
        /// <list type="number">
        ///   <item>从 _sessions 字典中移除该会话。</item>
            ///   <item>向房间内其他玩家广播 Goodbye 消息，让他们把该玩家的实体从 Simulation 中移除。</item>
        /// </list>
        /// <para>
        /// 注意：此方法在 ClientSession.RecvLoop 的 finally 块中调用，
        /// 因此可能从接收线程触发，需要加锁保护共享数据。
        /// </para>
        /// </summary>
        /// <param name="session">已离开的客户端会话。</param>
        public void NotifySessionLeft(ClientSession session)
        {
            lock (_inputLock)
            {
                // 从活跃会话字典中移除该会话
                _sessions.Remove(session.SessionId);
            }
            Console.WriteLine($"[Server] session left: PlayerId={session.PlayerId}");
            // 给其他玩家广播 Goodbye，让客户端把这个玩家清掉。
            var goodbye = new GoodbyeMessage { Type = MessageType.Goodbye, PlayerId = session.PlayerId };
            BroadcastExcept(session, goodbye);
        }

        /// <summary>
        /// 处理从客户端收到的消息，根据消息类型分发到对应逻辑。
        /// <para>
        /// 目前支持的消息类型：
        /// </para>
        /// <list type="bullet">
        ///   <item>Hello：玩家报名消息，客户端告知自己的名称。</item>
        ///   <item>Input：玩家输入消息，包含本帧的移动方向和开火状态。</item>
        /// </list>
        /// </summary>
        /// <param name="session">发送消息的客户端会话。</param>
        /// <param name="kind">消息类型字节。</param>
        /// <param name="payload">MemoryPack 二进制载荷。</param>
        public void HandleMessage(ClientSession session, byte kind, byte[] payload)
        {
            switch (kind)
            {
                case MessageType.MsgHello:
                {
                    var msg = MemoryPackSerializer.Deserialize<HelloMessage>(payload);
                    session.PlayerName = msg.Name;
                    Console.WriteLine($"[Server] hello from PlayerId={session.PlayerId} name={msg.Name}");
                    break;
                }

                case MessageType.MsgInput:
                {
                    var msg = MemoryPackSerializer.Deserialize<InputMessage>(payload);
                    var input = new PlayerInput(
                        session.PlayerId,
                        msg.Tick,
                        (byte)msg.MoveDir,
                        msg.Fire);
                    // 把这个输入登记到 _pendingInputs
                    lock (_inputLock)
                    {
                        _pendingInputs[input.PlayerId] = input;
                    }
                    break;
                }

                default:
                    Console.WriteLine($"[Server] unknown message kind: {kind}");
                    break;
            }
        }

        /// <summary>
        /// 生产一帧：这是帧同步服务端的核心方法，每个 tick 调用一次。
        /// <para>
        /// 执行流程：
        /// </para>
        /// <list type="number">
        ///   <item>递增 _currentTick，确定本帧的帧号。</item>
        ///   <item>收集本 tick 所有活跃玩家的输入；缺席玩家填充空输入（防止客户端卡死）。</item>
        ///   <item>将本帧的输入交给服务端 Simulation 推进一步（用于调试对比）。</item>
        ///   <item>将 FrameData 广播给所有连接的玩家。</item>
        /// </list>
        /// <para>
        /// 关键设计点：
        /// - "补空帧"策略：如果某个玩家本 tick 没有上报输入（网络延迟/掉线），
        ///   服务端自动为其生成一个空输入（MoveDir=0, Fire=false），保证帧数据完整。
        /// - 输入的 Tick 修正：客户端上报的 Tick 可能与服务端不同步（网络延迟），
        ///   服务端统一将输入的 Tick 修正为当前服务端 Tick，保证所有输入属于同一帧。
        /// </para>
        /// </summary>
        private void ProduceOneFrame()
        {
            // 帧号递增
            _currentTick++;
            int t = _currentTick;

            // 创建本帧的帧数据对象
            var frame = new FrameData(t);

            // 1) 收集本 tick 所有活跃玩家的输入。缺席玩家填空输入。
            //    这是帧同步服务端"防止客户端卡死自己"的"补空帧"策略。
            lock (_inputLock)
            {
                foreach (var kv in _sessions)
                {
                    int pid = kv.Value.PlayerId;
                    if (_pendingInputs.TryGetValue(pid, out var input))
                    {
                        // 若客户端填的 Tick 与服务端目标 Tick 不一致（网络延迟），以服务端 Tick 为准。
                        // 这样做的目的是保证所有输入都属于同一个帧，避免因客户端时钟偏差导致逻辑不一致。
                        input.Tick = t;
                        frame.Inputs.Add(input);
                    }
                    else
                    {
                        // 该玩家本 tick 没有上报输入 → 补一个空输入
                        // 空输入的含义：不移动、不开火，但仍然占位，保证帧数据中包含所有玩家
                        frame.Inputs.Add(PlayerInput.Empty(pid, t));
                    }
                }
                // 这个 tick 处理完输入，清空；下一 tick 重新收集。
                _pendingInputs.Clear();
            }

            // 2) 服务端自己也跑一次 Simulation（确定性），方便日后做引用/对比 sandbox。
            //    所有客户端用同样的帧数据跑同样的模拟，如果结果不一致，就说明存在非确定性问题。
            _sim.Step(frame);

            // 3) 广播 FrameData 给所有连接。
            Broadcast(new FrameMessage { Type = MessageType.Frame, Frame = new FrameDataWrapper { Tick = t, Inputs = frame.Inputs } });

            // 节流日志：每 20 个 tick 打印一次，避免日志刷屏
            // 输出当前帧号、在线人数、世界状态哈希值（用于校验一致性）
            if (t % 20 == 0)
            {
                Console.WriteLine($"[Server] tick={t} players={_sessions.Count} hash={_sim.World.ComputeHash()}");
            }
        }

        /// <summary>
        /// 向房间内所有活跃玩家广播消息。
        /// <para>
        /// 实现要点：
        /// 先在锁内对 _sessions.Values 做快照（复制到新列表），再在锁外遍历发送。
        /// 这样做是为了避免在发送过程中长时间持有锁（发送可能耗时），导致 tick 线程被阻塞。
        /// </para>
        /// </summary>
        /// <param name="obj">要广播的消息对象。</param>
        private void Broadcast<T>(T obj)
        {
            // 先做快照，再遍历——避免在发送时持有锁
            List<ClientSession> snapshot;
            lock (_inputLock)
            {
                snapshot = new List<ClientSession>(_sessions.Values);
            }
            foreach (var s in snapshot)
            {
                // 只向存活的会话发送，避免向已断开的连接写数据引发异常
                if (s.IsAlive) s.Send(obj);
            }
        }

        /// <summary>
        /// 向房间内除指定会话外的所有活跃玩家广播消息。
        /// <para>
        /// 典型使用场景：玩家 A 离开时，向除 A 以外的所有玩家广播 Goodbye 消息。
        /// 同样采用"先快照再遍历"的策略避免长时间持锁。
        /// </para>
        /// </summary>
        /// <param name="except">需要排除的会话（通常是触发事件的玩家自己）。</param>
        /// <param name="obj">要广播的消息对象。</param>
        private void BroadcastExcept<T>(ClientSession except, T obj)
        {
            // 先做快照，再遍历——避免在发送时持有锁
            List<ClientSession> snapshot;
            lock (_inputLock)
            {
                snapshot = new List<ClientSession>(_sessions.Values);
            }
            foreach (var s in snapshot)
            {
                if (s == except) continue;  // 跳过排除的会话
                if (s.IsAlive) s.Send(obj); // 只向存活的会话发送
            }
        }
    }
}
