using System;

namespace LockstepShared
{
    /// <summary>
    /// 逻辑节拍器 (TickTimer)。
    ///
    /// 单纯 Thread.Sleep 是无法做到稳定 tick 的：操作系统调度抖动会让 50ms 的睡眠变成 53ms 或 48ms。
    /// 这种抖动会"吃掉"帧率。TickTimer 给出新手可看懂的简单做法：
    ///   - 不用 sleep 来精确节拍；而是用"目标截止时间 (deadline)"驱动 tick 数。
    ///   - Update() 每次返回"这一帧应该 tick 几次 World.Step"，把抖动累计成整数 tick，避免飘移。
    ///
    /// 这种写法是 Unity 客户端里实战广泛使用的固定时间步的简化版。
    ///
    /// 原理详解：
    ///   假设 TickIntervalMs = 50ms（即 20 fps 逻辑帧率）：
    ///   - 第 1 次调用 Update() 时，距离上次已经过了 53ms → 返回 1 次 tick，基准时间推进 50ms
    ///   - 第 2 次调用 Update() 时，距离基准时间只过了 3ms → 返回 0 次 tick（等待）
    ///   - 第 3 次调用 Update() 时，距离基准时间过了 51ms → 返回 1 次 tick
    ///   - 长期来看，每 50ms 恰好产生 1 次 tick，不受偶尔的抖动影响
    ///
    /// 使用方式：
    ///   在主循环（服务端或客户端渲染循环）中每帧调用 Update()，
    ///   根据返回值决定调用几次 Simulation.Step()。
    ///
    ///   示例代码：
    ///   <code>
    ///   var timer = new TickTimer(GameConfig.TickIntervalMs);
    ///   while (running)
    ///   {
    ///       int ticks = timer.Update();
    ///       for (int i = 0; i &lt; ticks; i++)
    ///       {
    ///           simulation.Step(currentFrame);
    ///       }
    ///   }
    ///   </code>
    /// </summary>
    public class TickTimer
    {
        /// <summary>
        /// 目标步长（tick 间隔，毫秒）。
        /// 例如 GameConfig.TickRate=20 时，_intervalMs = 50ms，即每 50 毫秒推进一次逻辑帧。
        /// </summary>
        private readonly double _intervalMs;

        /// <summary>
        /// 上一次 tick 的"目标截止时间"。
        /// 不是上一次调用 Update 的真实时间，而是按 interval 对齐后的"理论时间"。
        /// 通过让 _lastTime 每次推进固定的 _intervalMs，来保证长期累积的 tick 数准确。
        /// </summary>
        private DateTime _lastTime;

        /// <summary>
        /// 最多一帧追赶的 tick 数，防止启动/卡顿后"一次性 tick 巨多"导致客户端逻辑卡爆。
        ///
        /// 为什么需要限制？
        ///   假设程序卡了 500ms（10 个 tick 的量），如果不限制：
        ///   - Update() 会返回 10，客户端需要在一帧内执行 10 次 Simulation.Step
        ///   - 每次_step 可能有复杂的逻辑运算，10 次叠加会导致本帧渲染卡顿
        ///   - 用户会感觉到明显的"画面冻结"
        ///   限制为 5 次后，客户端只会追 5 帧，剩余的会在后续帧继续追赶，
        ///   用"缓慢追赶"换取"不卡顿"。
        /// </summary>
        private const int MAX_TICKS_PER_UPDATE = 5;

        /// <summary>
        /// 构造 TickTimer，指定每个逻辑帧的间隔时间。
        /// </summary>
        /// <param name="intervalMs">逻辑帧间隔，单位毫秒。通常传入 GameConfig.TickIntervalMs。</param>
        public TickTimer(int intervalMs)
        {
            _intervalMs = intervalMs;
            _lastTime = DateTime.UtcNow;    // 用 UTC 时间避免时区切换导致的时间跳变
        }

        /// <summary>
        /// 调用方在外层渲染循环里每帧调用 Update，获得"本帧要 tick 多少次时钟"的整型 count。
        ///
        /// 算法原理：
        ///   1. 计算当前时间与 _lastTime 的差值（elapsed）
        ///   2. 如果 elapsed >= interval，说明应该产生一次 tick
        ///   3. 每产生一次 tick，_lastTime 推进一个 interval（而不是直接设为 now）
        ///      ——这样做是为了防止"误差累积"
        ///   4. 重复步骤 2-3 直到 elapsed < interval 或达到最大追赶次数
        ///   5. 如果卡顿太久（超过最大追赶量的 2 倍），直接重置基准时间，放弃追赶
        /// </summary>
        /// <returns>本帧应该执行的逻辑 tick 次数。0 表示不需要 tick，1 表示 tick 一次，以此类推。</returns>
        public int Update()
        {
            // 获取当前 UTC 时间
            var now = DateTime.UtcNow;
            // 计算自上次基准时间以来经过的毫秒数
            var elapsed = (now - _lastTime).TotalMilliseconds;

            int ticks = 0;
            // 有多少个 interval 就 tick 多少次，且把已经发放的时间从 _lastTime 里减掉。
            // 这样长期下来 tick 数 = 真实墙钟时间 / interval，是稳定的。
            //
            // 为什么用 while 而不是直接除法？
            //   因为每次 tick 后需要更新 _lastTime（推进一个 interval），
            //   然后重新计算 elapsed。如果一次推进了多个 interval，
            //   用除法无法正确更新 _lastTime 到最后一个 interval 的边界。
            while (elapsed >= _intervalMs && ticks < MAX_TICKS_PER_UPDATE)
            {
                // _lastTime 推进一个 interval（而非跳到 now），保证长期精度。
                // 例如：interval=50ms，实际过了 53ms，
                // 推进后 _lastTime 距 now 只有 3ms，下次 Update 时这 3ms 不会被浪费。
                _lastTime = _lastTime.AddMilliseconds(_intervalMs);
                elapsed = (now - _lastTime).TotalMilliseconds;
                ticks++;
            }
            // 如果空过很久/超出 max，重置基准时间，避免永久在追。
            // 判断条件：elapsed > interval * MAX * 2，即积压了超过最大追赶量 2 倍的 tick。
            // 例如 interval=50ms, MAX=5, 则积压超过 500ms 时直接重置。
            // 这意味着程序长时间暂停后（如最小化窗口），不会疯狂追赶，
            // 而是从当前时刻重新开始计时。
            if (elapsed > _intervalMs * MAX_TICKS_PER_UPDATE * 2)
            {
                _lastTime = now;
            }
            return ticks;
        }

        /// <summary>
        /// 返回"距离下一个 tick 还有多少毫秒"，供外层渲染/逻辑安排等待用。
        ///
        /// 使用场景：
        ///   - 服务端：在等待下一个 tick 期间，可以用 MillisUntilNextTick() 做 Sleep，
        ///     避免空转浪费 CPU
        ///   - 客户端：可以用来判断是否需要在渲染帧之间插入逻辑帧
        /// </summary>
        /// <returns>距离下一个逻辑 tick 的剩余毫秒数。如果已经到期则返回 0。</returns>
        public double MillisUntilNextTick()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastTime).TotalMilliseconds;
            // 如果已经超时（elapsed >= interval），返回 0（表示应该立即 tick）
            // 否则返回剩余等待时间
            return Math.Max(0, _intervalMs - elapsed);
        }
    }
}
