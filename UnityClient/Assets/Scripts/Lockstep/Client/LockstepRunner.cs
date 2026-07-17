using System;
using System.Collections.Generic;
using LockstepShared;

namespace Lockstep.Client
{
    /// <summary>
    /// 帧同步客户端业务核心（LockstepRunner）。
    /// <para>
    /// 这是整个帧同步学习示例中"含金量最高"的类，实现了客户端预测回滚的核心逻辑。
    /// 帧同步（Lockstep）的核心思想是：所有客户端从同一初始状态出发，按相同顺序执行相同输入，
    /// 从而保证最终状态一致。但网络延迟会导致客户端无法立即拿到其他玩家的输入，
    /// 所以需要"预测"来保证本地手感，等权威帧到达后再"回滚对账"纠正偏差。
    /// </para>
    /// <para>
    /// 本类维护两个独立的游戏世界（Simulation）：
    ///   1) _authoritativeSim（权威世界）：仅由服务端 FrameData 推进，代表"真正的游戏状态"；
    ///   2) _predictedSim（预测世界）：基于本地输入 + 最近权威状态 + 假设其他玩家不变 来推测前进，
    ///      玩家实际看到的画面就来自这个世界。
    /// </para>
    /// <para>
    /// 每个客户端 tick 的工作流程：
    ///   1. 处理服务端权威帧：将按序到达的 FrameData 推进 _authoritativeSim；
    ///   2. 构造本帧自己的输入并立即上报给服务端（通过 OnSendInput 钩子）；
    ///   3. 预测推进：将 _predictedSim 重建为 _authoritativeSim 的快照，
    ///      然后用"权威 tick 之后的所有本地输入历史"逐帧重放，直到 targetTick；
    ///   4. 对账：比较权威世界和预测世界的一致性。
    /// </para>
    /// <para>
    /// 预测回滚的本质——一句话总结：
    ///   "把每一帧本地输入当下跑一遍称为预测；收到权威帧发现差异就丢弃预测重跑。"
    /// </para>
    /// </summary>
    public class LockstepRunner
    {
        /// <summary>
        /// 自身的玩家 ID。在收到服务端 Welcome 消息时由上层设置确定。
        /// 初始值为 -1，表示尚未收到服务端分配的 ID。
        /// </summary>
        public int MyPlayerId = -1;

        /// <summary>
        /// 权威世界模拟器：以服务端 FrameData 推进，代表"真正的游戏世界"。
        /// <para>
        /// 这个模拟器只在收到服务端权威帧时才推进，不接受本地预测输入。
        /// 它的状态是"服务端认证过的"，可以视为真理（ground truth）。
        /// </para>
        /// </summary>
        private readonly Simulation _authoritativeSim = new Simulation();

        /// <summary>
        /// 预测世界模拟器：本地先跑，给玩家呈现即时手感。
        /// <para>
        /// 每次"服务器帧到达并追平"后，会被重建为权威世界的最新副本，
        /// 然后基于本地输入历史重新推演。这样即使预测有偏差，
        /// 收到权威帧后也能纠正回来，保证最终一致性。
        /// </para>
        /// </summary>
        private readonly Simulation _predictedSim = new Simulation();

        /// <summary>
        /// 已知的服务端最新 tick 号。
        /// <para>
        /// 客户端 tick 的目标是把自己的逻辑帧推进到 LatestServerTick + 1，
        /// 即始终比服务端已知帧超前 1 帧（用于预测）。
        /// </para>
        /// </summary>
        public int LatestServerTick { get; private set; } = 0;

        /// <summary>
        /// 本地输入历史缓存：记录每个 tick 自己上报的本地输入。
        /// <para>
        /// 键为 tick 号，值为该 tick 自己的 PlayerInput。
        /// 用途：当预测世界需要回滚重放时，从这里取出历史输入逐帧重新推演。
        /// </para>
        /// <para>
        /// 历史记录会定期清理（保留最近 200 帧），避免内存无限增长。
        /// </para>
        /// </summary>
        private readonly Dictionary<int, PlayerInput> _myInputHistory = new Dictionary<int, PlayerInput>();

        /// <summary>
        /// 已收到的尚未消费的服务端 FrameData 队列（按 tick 排序）。
        /// <para>
        /// 收到服务端 Frame 时不立即应用，而是入队，由 ClientTick 中按 tick 顺序消费。
        /// 这样可以避免收到乱序的未来帧导致状态错乱。
        /// </para>
        /// </summary>
        private readonly Queue<FrameData> _pendingServerFrames = new Queue<FrameData>();

        /// <summary>
        /// 客户端节拍定时器：控制 tick 的执行频率。
        /// <para>
        /// 间隔由 GameConfig.TickIntervalMs 决定（通常为 50ms，即 20 tick/秒）。
        /// Update() 方法通过此定时器判断是否需要执行新的 tick。
        /// </para>
        /// </summary>
        private readonly TickTimer _clientTimer = new TickTimer(GameConfig.TickIntervalMs);

        /// <summary>
        /// 本帧采集到的移动方向字节位。
        /// <para>
        /// 由外部（如 Program.cs 或 LockstepUnityBridge）在每帧 tick 之前刷新。
        /// 方向编码：bit0=下(1) bit1=上(2) bit2=左(4) bit3=右(8)，可组合。
        /// 例如：3 = 上+下，6 = 上+左，10 = 上+右。
        /// </para>
        /// </summary>
        public byte CurrentMoveDir;

        /// <summary>
        /// 本帧采集到的开火标志。
        /// <para>
        /// 由外部在每帧 tick 之前刷新。true 表示本帧按下了开火键。
        /// </para>
        /// </summary>
        public bool CurrentFire;

        /// <summary>
        /// 权威世界 tick 完成后的回调事件。
        /// <para>
        /// 每次权威模拟器推进一帧后触发，参数为推进后的世界状态。
        /// 上层可在此渲染权威世界画面或进行其他处理。
        /// </para>
        /// </summary>
        public Action<WorldState> OnAuthoritativeTick;

        /// <summary>
        /// 预测世界 tick 完成后的回调事件。
        /// <para>
        /// 每次预测模拟器推进一帧后触发，参数为推进后的世界状态。
        /// 上层可在此渲染预测世界画面。通常玩家看到的是预测世界。
        /// </para>
        /// </summary>
        public Action<WorldState> OnPredictedTick;

        /// <summary>
        /// ★ 关键钩子：把"本帧我想做的输入"发给服务端的回调。
        /// <para>
        /// 这个钩子由上层（Program.cs 或 LockstepUnityBridge）注入；
        /// LockstepRunner 不直接依赖 NetClient，保持内核与网络层解耦。
        /// 每个本地 tick 调用一次（频率 = TickRate），由 NetClient 实际发送消息。
        /// </para>
        /// <para>
        /// 设计说明：真实项目会对它做"ack/batch/插值补偿"等优化，
        /// 本示例为最简实现——每 tick 发一次输入。
        /// </para>
        /// </summary>
        public Action<PlayerInput> OnSendInput;

        /// <summary>
        /// 初始化玩家池：将指定的玩家 ID 添加到权威世界和预测世界的模拟器中。
        /// <para>
        /// 在收到服务端 Welcome 消息时，需要把所有已在房间中的玩家（包括自己）都加进来；
        /// 收到 PlayerJoin 消息时，需要把新加入的玩家添加进来。
        /// 两个模拟器必须保持相同的玩家集合，否则预测和权威状态无法对比。
        /// </para>
        /// </summary>
        /// <param name="playerIds">要添加的玩家 ID 列表</param>
        public void InitPlayers(List<int> playerIds)
        {
            foreach (var pid in playerIds)
            {
                // 同时添加到权威和预测两个模拟器中
                _authoritativeSim.AddPlayer(pid);
                _predictedSim.AddPlayer(pid);
            }
        }

        /// <summary>
        /// ★ 晚加入对齐：把权威世界和预测世界的 tick 跳到服务端当前 tick。
        /// <para>
        /// 这是"晚加入"场景的关键方法。如果不做这一步：
        ///   - 客户端权威 tick 从 0 开始；
        ///   - 收到服务端 tick=161 的帧时，判断 161 != 0+1，直接 break；
        ///   - 所有帧积压 → 永远消费不掉 → 坐标永远为 0。
        /// </para>
        /// <para>
        /// 对于"服务端一直在跑但无玩家"的阶段，跳过的都是空帧，
        /// 世界状态不变，所以直接对齐 tick 是安全的。
        /// </para>
        /// </summary>
        /// <param name="serverTick">服务端当前 tick 号（从 Welcome 消息中获取）</param>
        public void InitFromServerTick(int serverTick)
        {
            _authoritativeSim.World.Tick = serverTick;  // 对齐权威世界 tick
            _predictedSim.World.Tick = serverTick;      // 对齐预测世界 tick
            LatestServerTick = serverTick;               // 更新已知的服务端最新 tick
        }

        /// <summary>
        /// 获取权威世界的当前状态（只读属性）。
        /// <para>
        /// 权威世界代表服务端认证过的"真实游戏状态"。
        /// </para>
        /// </summary>
        public WorldState AuthoritativeWorld => _authoritativeSim.World;

        /// <summary>
        /// 获取预测世界的当前状态（只读属性）。
        /// <para>
        /// 预测世界是玩家实际看到的画面来源，基于本地输入推测前进。
        /// </para>
        /// </summary>
        public WorldState PredictedWorld => _predictedSim.World;

        /// <summary>
        /// 收到服务端 FrameData 时的处理入口。
        /// <para>
        /// 我们不立即应用帧数据，而是入队到 _pendingServerFrames 中，
        /// 由 ClientTick 按严格的 tick 顺序消费。这样可以：
        ///   - 避免收到未来帧导致状态跳变；
        ///   - 保证权威世界始终按 tick 单调递增的顺序推进；
        ///   - 处理网络乱序的情况。
        /// </para>
        /// </summary>
        /// <param name="frame">从服务端收到的帧数据，包含 tick 号和所有玩家输入</param>
        public void OnReceiveServerFrame(FrameData frame)
        {
            _pendingServerFrames.Enqueue(frame);
        }

        /// <summary>
        /// 外部 Update 钩子：由主线程的固定更新循环调用（如 Unity Update 或控制台主循环）。
        /// <para>
        /// 通过 _clientTimer 判断是否需要执行新的 tick，
        /// 如果需要则执行对应次数的 ClientTick()。
        /// 这样可以保证逻辑帧率稳定，不受渲染帧率影响。
        /// </para>
        /// </summary>
        public void Update()
        {
            // 计算自上次 Update 以来需要执行多少次 tick
            int ticks = _clientTimer.Update();
            for (int i = 0; i < ticks; i++)
            {
                ClientTick();
            }
        }

        /// <summary>
        /// 客户端逻辑帧的核心方法：每个 tick 执行一次。
        /// <para>
        /// 执行流程分为 4 步：
        ///   第 1 步：处理服务端权威帧——从队列中按序消费，推进权威世界；
        ///   第 2 步：构造本帧自己的输入，记录到历史并立即上报服务端；
        ///   第 3 步：预测推进——重建预测世界，用本地输入历史重放到目标 tick；
        ///   第 4 步：对账——比较权威和预测世界的一致性；
        ///   最后：清理过期的输入历史。
        /// </para>
        /// </summary>
        private void ClientTick()
        {
            // 本 tick 的目标帧号：比服务端最新 tick 超前 1 帧
            // 这 1 帧的"超前"就是客户端预测的空间
            int targetTick = LatestServerTick + 1;

            // === 第 1 步：处理服务端权威帧 ===
            // 把队列里"tick == 本地权威 tick+1"的帧挨个消费进 _authoritativeSim。
            // 严格按 tick 顺序消费：只接受"紧接着下一帧"的帧，
            // 超过 tick+1 的未来帧留队等下一 tick（保证不乱序），
            // 低于当前 tick 的过期帧直接丢弃。
            while (_pendingServerFrames.Count > 0)
            {
                var next = _pendingServerFrames.Peek();  // 窥视队首帧，不取出

                if (next.Tick == _authoritativeSim.World.Tick + 1)
                {
                    // 正好是下一帧：消费它，推进权威世界
                    _authoritativeSim.Step(next);
                    LatestServerTick = next.Tick;        // 更新已知的服务端最新 tick
                    _pendingServerFrames.Dequeue();      // 从队列中移除已消费的帧
                    OnAuthoritativeTick?.Invoke(_authoritativeSim.World);  // 通知上层

                    // 诊断日志：每 20 帧输出一次，确认权威帧在消费
                    // 使用 Console.Error 输出，不受 SetCursorPosition 影响
                    if (next.Tick % 20 == 0)
                        Console.Error.WriteLine($"[AUTH] tick={next.Tick} players={_authoritativeSim.World.Players.Count}");
                }
                else if (next.Tick <= _authoritativeSim.World.Tick)
                {
                    // 过期帧：权威 tick 已经超过它了，直接丢弃
                    // 这种情况可能发生在网络重传或客户端回退后重复收到旧帧时
                    _pendingServerFrames.Dequeue();
                }
                else
                {
                    // 出现"跳号"：队首帧的 tick > 权威 tick + 1
                    // 可能是丢包导致中间帧还没到达，等后续帧补上后再消费
                    break;
                }
            }

            // === 第 2 步：构造本 tick "我自己的输入"并立即上报 ===
            // 将外部设置的当前输入（CurrentMoveDir / CurrentFire）封装为 PlayerInput
            var myInput = new PlayerInput(MyPlayerId, targetTick, CurrentMoveDir, CurrentFire);
            // 记录到输入历史，回滚时需要按序重放
            _myInputHistory[targetTick] = myInput;

            // ★ 真正派出网络包：如果上层注入了 OnSendInput，每 tick 一发；
            //   服务端会在自己的对应 tick 把它装进 FrameData 再广播回来。
            OnSendInput?.Invoke(myInput);
            // 真实项目应在这里加"发送频率限制 / batch"优化，
            // 例如：不是每 tick 都发，而是攒几帧一起发，减少网络包数量。

            // === 第 3 步：预测推进 ===
            // 规则：
            //   1. 把 _predictedSim 重建为最新权威世界的副本（丢弃旧的预测结果）；
            //   2. 从最新权威 tick 之后开始，用所有本地输入逐帧推演预测世界；
            //   3. 一直推到 tick == targetTick 为止。
            //
            // 这个"重建+重放"的过程就是"回滚对账"的核心：
            //   - 如果预测和权威一致，重放结果与之前预测相同，玩家无感知；
            //   - 如果权威帧带来了新信息（其他玩家输入），重放后预测会被纠正。
            RebuildPredictedFromAuthoritative();

            while (_predictedSim.World.Tick < targetTick)
            {
                int pt = _predictedSim.World.Tick + 1;  // 当前要预测的帧号

                // 构造预测帧：复用一个空 FrameData 容器，
                // 把"本地输入 + 其他玩家空输入"塞进它
                var predictedFrame = new FrameData(pt);

                // 遍历权威世界中的所有玩家，为每个玩家构造输入
                // 使用权威世界的玩家集合作为"完整玩家列表"，保证不遗漏
                foreach (var kv in _authoritativeSim.World.Players)
                {
                    int pid = kv.Key;
                    if (pid == MyPlayerId)
                    {
                        // 本地玩家：从输入历史中取出自己该帧的输入
                        // 这一帧的输入刚刚被记到 _myInputHistory 中，必然能命中
                        if (_myInputHistory.TryGetValue(pt, out var h))
                            predictedFrame.Inputs.Add(h);
                        else
                            predictedFrame.Inputs.Add(PlayerInput.Empty(pid, pt));  // 兜底：空输入
                    }
                    else
                    {
                        // 其他玩家：用空输入兜底
                        // 这是最朴素的预测——假设其他玩家在我们不知道的时候是静止的。
                        // 高级实现会使用"惯性预测"（重复使用上次已知输入）来提升一致性，
                        // 减少回滚带来的画面跳动。
                        predictedFrame.Inputs.Add(PlayerInput.Empty(pid, pt));
                    }
                }

                // 用构造的预测帧推进预测世界
                _predictedSim.Step(predictedFrame);
                // 通知上层预测世界已推进
                OnPredictedTick?.Invoke(_predictedSim.World);
            }

            // === 第 4 步：对账（异步报告哈希差）===
            // 一旦服务端权威 tick 上来了，且对应本地输入有记录，
            // 就可以做哈希 diff，检查预测是否与权威一致。
            // 本示例仅预留了位置，未做完整的哈希对比和错误回退。
            // 读者可结合 WorldState.ComputeHash 试着实现量化指标。
            if (LatestServerTick > 0 &&
                _myInputHistory.TryGetValue(LatestServerTick, out _))
            {
                // 真实判断：把"权威世界"和"同一 tick 的预测世界"的哈希做对比。
                // 如果不一致，说明预测有偏差，需要记录或纠正。
                // 本示例仅做预留，读者可自行扩展。
            }

            // 历史 tick 老化清理：保留最近 200 帧 tick 的本地输入，避免内存无限增长。
            // 200 帧约等于 10 秒（20 tick/秒），足够覆盖任何合理的网络延迟。
            long keepFloor = (long)LatestServerTick - 200;
            if (keepFloor > 0)
            {
                // 收集所有过期的 tick 键
                var stale = new List<int>();
                foreach (var kv in _myInputHistory)
                {
                    if (kv.Key < keepFloor) stale.Add(kv.Key);
                }
                // 批量移除过期记录
                foreach (var k in stale) _myInputHistory.Remove(k);
            }
        }

        /// <summary>
        /// 将预测世界重建为权威世界的最新快照。
        /// <para>
        /// 每次预测前都调用此方法重置预测世界，确保预测从最新的权威状态开始。
        /// 这是"回滚"操作的核心——丢弃旧的预测结果，从权威状态重新出发。
        /// </para>
        /// <para>
        /// 实现原理：
        ///   1. 对权威世界做一次深拷贝（CloneSnapshot）；
        ///   2. 将拷贝设置为预测世界的状态（SetWorld）。
        /// 这样预测世界就从权威世界的最新状态开始，可以基于本地输入重新推演。
        /// </para>
        /// </summary>
        private void RebuildPredictedFromAuthoritative()
        {
            // 对权威世界做深拷贝，得到独立的状态快照
            var snap = _authoritativeSim.World.CloneSnapshot();
            // 将预测世界替换为权威世界的快照
            // 注意：这要求 Simulation 暴露"直接替换世界"的接口，通过 SetWorld 实现
            _predictedSim.SetWorld(snap);
        }
    }

    /// <summary>
    /// Simulation 的扩展方法类，为支持回滚提供"直接替换世界状态"的接口。
    /// <para>
    /// 仅限客户端的 LockstepRunner 调用，不在服务端使用。
    /// 服务端的世界状态是顺序推进的，不需要替换；
    /// 客户端则需要频繁回滚（重建预测世界），所以需要这个接口。
    /// </para>
    /// </summary>
    public static class SimulationExtension
    {
        /// <summary>
        /// 直接替换 Simulation 的世界状态。
        /// <para>
        /// 该扩展方法是对 Simulation 内部 SetWorld 方法的包装。
        /// 因为 Simulation.World 属性的 setter 是 private 的，
        /// 外部无法直接赋值，所以需要通过这个方法间接调用。
        /// </para>
        /// <para>
        /// 设计说明：本示例采用"最小侵入面"原则，
        /// 只在 Simulation 中公开了一个 SetWorld 方法，
        /// 而不是把 World 属性的 setter 设为 public。
        /// </para>
        /// </summary>
        /// <param name="sim">要替换世界状态的模拟器实例</param>
        /// <param name="world">新的世界状态（通常是权威世界的深拷贝快照）</param>
        public static void SetWorld(this Simulation sim, WorldState world)
        {
            // 委托调用 Simulation 内部的 SetWorld 方法
            sim.SetWorld(world);
        }
    }
}
