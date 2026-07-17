namespace LockstepShared
{
    /// <summary>
    /// 全局常量配置。所有"会影响两边行为"的常量都集中在这里，避免服务端客户端对不上。
    ///
    /// 为什么要把配置集中在一个类里？
    ///   帧同步最大的敌人就是"服务端和客户端行为不一致"。
    ///   如果服务端用 TickRate=20 而客户端硬编码 TickRate=30，
    ///   两端的速度计算就完全不同，帧同步必然失败。
    ///   把所有配置常量集中在 Shared 项目的 GameConfig 中，
    ///   确保两端编译时引用的是同一份值。
    ///
    /// 使用原则：
    ///   - 所有影响游戏逻辑的数值都必须定义在此类中
    ///   - 不要在 Simulation 或其他逻辑代码中硬编码任何"魔法数字"
    ///   - 修改配置值时，两端会自动同步（因为共享同一份源码）
    /// </summary>
    public static class GameConfig
    {
        /// <summary>
        /// 逻辑帧率：每秒推进多少次世界逻辑 (tick)。20 表示 50ms / 帧。
        ///
        /// 帧同步里这个值要"远低于渲染帧率"，给网络留出往返时间。
        /// 为什么？
        ///   - 渲染帧率通常 60fps (16.7ms/帧)，而逻辑帧率 20fps (50ms/帧)
        ///   - 50ms 的时间窗口内，客户端需要：采集输入 → 发送给服务端 → 等服务端广播
        ///   - 如果逻辑帧率太高（如 60fps），每帧只有 16.7ms，网络延迟很容易超时
        ///   - 20fps 是帧同步的常见选择，兼顾了操作流畅度和网络容错空间
        ///
        /// 注意：此值与 Simulation.Step 中的速度计算直接相关：
        ///   delta = MoveSpeed / TickRate，TickRate 变化会影响移动速度。
        /// </summary>
        public const int TickRate = 20;

        /// <summary>
        /// 每帧逻辑时长（毫秒）。= 1000 / TickRate = 1000 / 20 = 50ms。
        ///
        /// 这个值被 TickTimer 使用来控制逻辑帧的节拍，
        /// 也被服务端用来确定输入收集窗口的时长。
        /// </summary>
        public const int TickIntervalMs = 1000 / TickRate;

        /// <summary>
        /// 地图边界宽度（定点数单位，即"图块"数）。超过此值的 X 坐标会被夹紧到此值。
        ///
        /// 例如 MapWidth=20 表示地图从 X=0 到 X=20，宽度为 20 个图块。
        /// 在 Simulation.Step 的边界夹紧逻辑中使用：
        ///   if (player.X.Raw > MapWidth * FixedPoint.QUANTA) player.X = ...
        /// </summary>
        public const int MapWidth = 20;

        /// <summary>
        /// 地图边界高度（定点数单位，即"图块"数）。超过此值的 Y 坐标会被夹紧到此值。
        ///
        /// 与 MapWidth 含义相同，控制 Y 轴方向的边界。
        /// 当前地图是正方形的（宽=高=20），但分开定义方便后续扩展为矩形地图。
        /// </summary>
        public const int MapHeight = 20;

        /// <summary>
        /// 玩家移动速度，单位"图块/秒"（定点数后会 * QUANTA）。
        ///
        /// 例如 MoveSpeed=4.0 表示玩家每秒移动 4 个图块距离。
        /// 在 Simulation.Step 中通过以下公式计算每帧移动距离：
        ///   delta = MoveSpeed / TickRate = 4.0 / 20 = 0.2 图块/帧
        ///   即每帧移动 0.2 个图块，每秒 20 帧 × 0.2 = 4 图块/秒 ✓
        ///
        /// 注意：这里定义为 float 是因为配置值天然是浮点数（如 4.0），
        /// 在 Simulation.Step 中会通过 FixedPoint.FromFloat 转换为定点数再参与计算。
        /// </summary>
        public const float MoveSpeed = 4.0f;

        /// <summary>
        /// 服务端为每一帧收集输入的"输入窗口"时长，单位 ms。
        ///
        /// 超过这个窗口还没上报输入的玩家，本帧用"空输入"兜底，避免卡死所有人。
        /// 当前值 = TickIntervalMs (50ms)，意味着服务端每个逻辑帧只等 50ms。
        ///
        /// 设计权衡：
        ///   - 设太短（如 10ms）：网络稍慢的玩家总是空输入，体验极差
        ///   - 设太长（如 200ms）：所有玩家都要等最慢的那个，操作延迟明显
        ///   - = TickIntervalMs 是最紧凑的选择，也是本示例的简化方案
        ///   - 生产项目通常设为 TickIntervalMs 的 1.5~2 倍，给网络波动留余量
        /// </summary>
        public const int InputCollectWindowMs = TickIntervalMs;

        /// <summary>
        /// 服务端广播节拍：每隔多少 ms 产一帧。
        ///
        /// 本示例里直接 = TickIntervalMs，即服务端以与逻辑同频跑。
        /// 真实项目里会有"输入 buffer + 超时补空输入"等容错策略。
        ///
        /// 为什么服务端需要自己的广播节拍？
        ///   服务端不是"一收到输入就广播"，而是按固定节拍统一产帧。
        ///   这样保证：
        ///   1. 所有玩家的输入在同一时间窗口内被收集
        ///   2. 广播频率稳定，客户端不会忽快忽慢
        ///   3. 即使某个玩家没发输入，也能按时产帧（用空输入补位）
        /// </summary>
        public const int ServerTickMs = TickIntervalMs;
    }
}
