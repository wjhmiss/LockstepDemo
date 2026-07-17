using System.Collections.Generic;

namespace LockstepShared
{
    /// <summary>
    /// 确定性模拟器 (Deterministic Simulation)。
    ///
    /// 这是整个帧同步项目的"心脏"。它的核心要求：
    ///   1. 给定同样的初始 WorldState 与同样的 FrameData，演化出的 WorldState 必须 byte-for-byte 完全一致。
    ///   2. 代码内部禁止任何"非确定性"因素：不依赖系统时间、不依赖随机、不依赖浮点。
    ///   3. 客户端和服务端共享同一份此类型/方法的代码（这就是把 Simulation 放在 Shared 项目里的原因）。
    ///
    /// 本示例模拟内容非常简单：玩家按方向键等速移动，四方向可叠加（左+上 = 左上斜走），
    /// 碰到地图边界则被夹紧。它足以演示帧同步全部环节，但又有意保持极简。
    ///
    /// 确定性三大纪律（必须严格遵守）：
    ///   1. 只用定点数（FixedPoint）做运算，禁止 float/double 参与逻辑
    ///   2. 不使用 System.Random、DateTime.Now 等非确定性来源
    ///   3. 遍历集合的顺序必须固定（Dictionary 按插入顺序遍历）
    ///
    /// 使用方式：
    ///   - 服务端：收到所有玩家输入后调用 Step(frame) 推进世界，然后广播 FrameData
    ///   - 客户端：收到服务端广播的 FrameData 后调用 Step(frame) 推进本地世界
    ///   - 客户端预测：在等待服务端广播时，用本地输入预测性调用 Step(frame)
    /// </summary>
    public class Simulation
    {
        /// <summary>
        /// 当前世界状态。Tick 表示"这是第几帧结束时"的状态。
        /// 每次调用 Step 后，World 的内容会被更新，Tick 会前进。
        /// </summary>
        public WorldState World { get; private set; }

        /// <summary>
        /// 构造模拟器，初始化一个空的世界状态。
        /// 新构造的 World 中没有玩家，需要通过 AddPlayer 逐个添加。
        /// </summary>
        public Simulation()
        {
            World = new WorldState();
        }

        /// <summary>
        /// 注册加入房间的玩家。服务端和客户端都要在加入时调用一次，保证两边状态字典结构一致。
        ///
        /// 为什么服务端和客户端都要调用？
        ///   因为 WorldState.Players 是一个字典，如果服务端添加了玩家但客户端没添加，
        ///   当客户端收到包含该玩家输入的 FrameData 时，会在 Step 中找不到该玩家。
        ///   虽然本示例有自动添加逻辑，但显式调用 AddPlayer 可以确保
        ///   两端的 Players 字典在游戏开始前就保持一致。
        /// </summary>
        /// <param name="playerId">要添加的玩家唯一标识。</param>
        public void AddPlayer(int playerId)
        {
            World.AddPlayer(playerId);
        }

        /// <summary>
        /// 直接替换世界状态。仅用于客户端预测回滚：把 _predictedSim 重置成最新权威副本。
        ///
        /// 不要在服务端 Simulation 上调用此方法；它专为客户端预测回滚准备。
        ///
        /// 使用场景：
        ///   客户端在预测回滚时，发现服务端的权威帧与本地预测不一致，
        ///   需要把本地世界状态"重置"到服务端的权威状态，
        ///   然后用正确的输入重新模拟。SetWorld 就是用来做这个"重置"的。
        /// </summary>
        /// <param name="world">要替换成的权威世界状态。通常来自服务端的广播。</param>
        public void SetWorld(WorldState world)
        {
            World = world;
        }

        /// <summary>
        /// 推进一帧世界逻辑。这是帧同步唯一的"驱动入口"。
        /// 调用方传入本帧的 FrameData（包含所有玩家输入），本方法据此修改 World 让其 Tick+1。
        ///
        /// 执行流程：
        ///   1. 遍历 FrameData 中每个玩家的输入
        ///   2. 对每个玩家：根据方向输入计算本帧位移，更新位置，夹紧到地图边界
        ///   3. 将 World.Tick 更新为 frame.Tick
        ///
        /// 注意：本函数必须严格执行"纯函数"的纪律——除了 World 自身，不允许读任何外部可变状态。
        /// 这意味着：
        ///   - 不能读取系统时间、随机数
        ///   - 不能读取非 World 拥有的可变静态变量
        ///   - 不能使用 float/double 进行逻辑计算
        ///   - 所有分支判断必须基于输入参数和 World 的当前值
        /// </summary>
        /// <param name="frame">本帧的输入数据，包含所有玩家的输入指令。</param>
        public void Step(FrameData frame)
        {
            // 1) 把每个玩家的输入落到对应实体上。
            //    - 缺席玩家的输入在服务端已被补成 PlayerInput.Empty，所以这里一定有每个玩家的数据。
            foreach (var input in frame.Inputs)
            {
                // 尝试从 World.Players 字典中获取对应玩家实体。
                if (!World.Players.TryGetValue(input.PlayerId, out var player))
                {
                    // ★ 自动添加：服务端广播了新玩家的输入但本地还没 AddPlayer。
                    //   典型场景：P2 在 P1 之后加入，P1 收到含 P2 输入的 FrameData 时
                    //   P2 还不在 P1 的 Players 字典里。如果不自动添加，P2 的输入会被
                    //   静默忽略 → P1 永远看不到 P2。
                    AddPlayer(input.PlayerId);
                    player = World.Players[input.PlayerId];
                }

                // 2) 根据方向位计算本帧位移。
                //    每秒速度 = MoveSpeed (图块/秒)；每帧时间 = 1/TickRate (秒)。
                //    delta = MoveSpeed / TickRate (图块/帧)，全程走整数运算。
                //
                //    ★ 确定性关键：这里用 FromFloat 把 TickRate 转成"值为 20.0 的定点数"，
                //      而不能用 new FixedPoint(TickRate)——后者会把 20 当作 Raw（= 0.020），
                //      导致 delta 被放大 ~1000 倍，玩家一帧就冲出地图被夹紧，表现为"一动不动"。
                //      这是因为 FixedPoint 构造函数的参数 raw 是"放大后的整数值"，
                //      new FixedPoint(20) 意味着实际值 0.020，而非 20.0。
                var speed = FixedPoint.FromFloat(GameConfig.MoveSpeed);
                var delta = speed / FixedPoint.FromFloat(GameConfig.TickRate);

                // 用累加 dx/dy 的方式表达"四方向可叠加"；如果只按方向键之一则单方向。
                // 初始值为 Zero，根据按下的方向键逐个累加位移量。
                FixedPoint dx = FixedPoint.Zero;
                FixedPoint dy = FixedPoint.Zero;

                // 一来一回：上(+y)、下(-y)、左(-x)、右(+x)。
                // 注意：地图坐标系用"数学坐标系"：Y 向上为正。客户端渲染层只需要照抄坐标即可。
                //
                // 为什么上和下要分别用 +delta 和 -delta？
                //   因为在数学坐标系中 Y 轴向上为正，所以"按上键"应该增加 Y 坐标，
                //   "按下键"应该减少 Y 坐标。同理 X 轴向右为正。
                if (input.Up)    dy += delta;
                if (input.Down)  dy -= delta;
                if (input.Left)  dx -= delta;
                if (input.Right) dx += delta;

                // 将本帧位移应用到玩家位置
                player.X += dx;
                player.Y += dy;

                // 边界夹紧：超出地图则裁回，确保任何机器求得的最终结果都一致。
                // 为什么需要夹紧？因为浮点/定点数没有天然的边界限制，
                // 如果不夹紧，玩家可能移动到负坐标或超出地图范围，
                // 导致渲染异常或逻辑错误。而且不同客户端如果不做一致的夹紧，
                // 同样的输入可能产生不同的位置，破坏帧同步。
                if (player.X < FixedPoint.Zero) player.X = FixedPoint.Zero;
                if (player.Y < FixedPoint.Zero) player.Y = FixedPoint.Zero;
                // 右/上边界：MapWidth * QUANTA 将地图宽度转为定点数的 Raw 值。
                // 例如 MapWidth=20 → Raw=20000，表示 X 坐标最大为 20.0。
                if (player.X.Raw > GameConfig.MapWidth  * FixedPoint.QUANTA) player.X = new FixedPoint(GameConfig.MapWidth  * FixedPoint.QUANTA);
                if (player.Y.Raw > GameConfig.MapHeight * FixedPoint.QUANTA) player.Y = new FixedPoint(GameConfig.MapHeight * FixedPoint.QUANTA);

                // 记录本帧的移动方向和开火状态，供渲染层使用
                player.LastMoveDir = (byte)input.MoveDir;
                player.FiredThisFrame = input.Fire;
            }

            // 3) 推进世界 tick。 Tick 表示这份 World 是"推进完第几帧"后的状态。
            //    使用 frame.Tick 而非 World.Tick+1，是因为在回滚重模拟场景中，
            //    frame.Tick 可能与 World.Tick+1 不一致（可能跳帧或回退）。
            World.Tick = frame.Tick;
        }
    }
}
