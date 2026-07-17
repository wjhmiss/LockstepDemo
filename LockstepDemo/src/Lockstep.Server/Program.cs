using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Lockstep.Server
{
    /// <summary>
    /// 服务端主程序入口类。
    /// <para>核心职责：</para>
    /// <list type="number">
    ///   <item>监听 TCP 端口，等待客户端连接。</item>
    ///   <item>每收到一个新连接，创建对应的 ClientSession 并交由 Room 管理。</item>
    ///   <item>Room 按固定时间节拍（tick）产生 FrameData（帧数据）并广播给所有玩家。</item>
    /// </list>
    /// <para>
    /// 启动方式：dotnet run --project src/Lockstep.Server -- [port]
    /// 默认端口为 7777。
    /// </para>
    /// <para>
    /// 整体架构说明：
    /// 本服务端采用"帧同步"（Lockstep）模型——服务端不计算游戏逻辑，
    /// 只负责收集所有玩家的输入，按固定频率打包成"帧"，然后广播给所有人。
    /// 每个客户端收到帧后，用相同的输入驱动各自的模拟（Simulation），
    /// 因为逻辑是确定性的，所以所有人的游戏状态始终一致。
    /// </para>
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 服务端主入口方法。
        /// <para>执行流程：</para>
        /// <list type="number">
        ///   <item>解析命令行参数获取端口号（默认 7777）。</item>
        ///   <item>创建 Room（房间）实例，所有玩家共享同一个房间。</item>
        ///   <item>启动后台 tick 线程，按固定节拍驱动房间产帧。</item>
        ///   <item>在主线程上监听 TCP 连接，每来一个连接就创建一个 ClientSession。</item>
        /// </list>
        /// </summary>
        /// <param name="args">命令行参数。第一个参数可选，表示监听端口号。</param>
        private static void Main(string[] args)
        {
            // 解析端口号：如果命令行提供了参数就用它，否则默认 7777
            int port = args.Length > 0 ? int.Parse(args[0]) : 7777;

            // 1) 创建房间：所有玩家共享一个房间；
            //    真实项目按房间 ID 分组，本示例只演示单房逻辑。
            var room = new Room();

            // 2) 启动房间 tick 线程：在后台按 GameConfig.ServerTickMs 节拍产 FrameData 并广播。
            //    主线程需在"Accept 阻塞"之前启动它，避免产帧被卡住。
            //    IsBackground = true 表示这是后台线程，主线程退出时它也会自动终止。
            new Thread(() => RoomTickLoop(room)) { IsBackground = true }.Start();

            // 3) 监听 TCP：绑定所有网络接口（IPAddress.Any）的指定端口。
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"[Server] listening on port {port}...");

            try
            {
                // 主循环：不断接受新的客户端连接
                while (true)
                {
                    // AcceptTcpClient() 是阻塞调用，会一直等待直到有新连接进来。
                    // 因此可以安全地放在主线程上运行，不会浪费 CPU。
                    var tcpClient = listener.AcceptTcpClient();

                    // 用 Interlocked.Increment 保证 SessionId 的自增是线程安全的
                    // （虽然目前只在主线程调用，但养成好习惯总没错）
                    var sessionId = Interlocked.Increment(ref SessionIdCounter);
                    Console.WriteLine($"[Server] new connection: id={sessionId} ep={tcpClient.Client.RemoteEndPoint}");

                    // 为每个连接创建一个 ClientSession，它会自动启动收发线程
                    var session = new ClientSession(sessionId, tcpClient, room);
                    // 将会话加入房间，房间会分配 PlayerId 并通知其他玩家
                    room.AddSession(session);
                }
            }
            catch (Exception ex)
            {
                // 捕获致命异常（如端口被占用、网络故障等），打印后退出
                Console.WriteLine($"[Server] fatal: {ex}");
            }
            finally
            {
                // 无论是否异常，都确保关闭 TCP 监听器，释放端口资源
                listener.Stop();
            }
        }

        /// <summary>
        /// 会话 ID 自增计数器。
        /// <para>
        /// 每个新连接都会获得一个唯一的 SessionId，从 1 开始递增。
        /// 使用 Interlocked.Increment 保证多线程下的安全自增。
        /// </para>
        /// </summary>
        private static int SessionIdCounter = 0;

        /// <summary>
        /// 房间 tick 后台循环：让 Room 按 GameConfig.ServerTickMs 稳定步进。
        /// <para>
        /// 这里的设计思路：
        /// </para>
        /// <list type="bullet">
        ///   <item>使用 Thread.Sleep(1) 进行短暂的休眠，避免忙等（busy-wait）吃满 CPU。</item>
        ///   <item>每次醒来调用 Room.Tick()，由内部的 TickTimer 判断是否该产一帧。</item>
        ///   <item>TickTimer 通过累加经过时间来计算"该执行几次 tick"，从而补偿因 Sleep 不精确带来的偏差。</item>
        /// </list>
        /// <para>
        /// 为什么不用 Thread.Sleep(固定帧间隔)?
        /// 因为 Thread.Sleep 的精度很低（Windows 上约 15ms），直接用会导致帧率不准。
        /// 用 Sleep(1) + TickTimer 的方式可以更精确地控制帧率。
        /// </para>
        /// </summary>
        /// <param name="room">需要驱动的房间实例。</param>
        private static void RoomTickLoop(Room room)
        {
            Console.WriteLine("[Server] room tick loop started.");
            while (true)
            {
                // 调用 Room.Tick()，内部 TickTimer 会根据经过时间决定是否产帧
                room.Tick();
                // 短睡一下避免忙等。具体一帧是否产生由 TickTimer 决定。
                Thread.Sleep(1);
            }
        }
    }
}
