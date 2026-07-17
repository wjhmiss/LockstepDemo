using System;

namespace LockstepShared
{
    /// <summary>
    /// 定点数 (Fixed-Point Number) —— 帧同步里"确定性"的基石。
    ///
    /// 为什么需要定点数？
    ///   不同机器、不同 CPU 上，浮点数 (float/double) 的运算结果在最后一位可能不同（IEEE 754 允许误差）。
    ///   这点小误差在帧同步中会被放大成肉眼可见的"不同步"。因此帧同步必须用"整数运算 + 固定小数位"来模拟小数。
    ///
    /// 本类用 long 存储放大后的整数值；QUANTA = 1000，即保留 3 位小数。
    /// 这是新手友好的简化实现，足够理解原理；生产环境可以用 Q32.32 / Look-ahead 修正等更精密的方案。
    ///
    /// 原理说明：
    ///   定点数的核心思想是"用整数模拟小数"。具体做法是将所有值放大 QUANTA 倍后存储为整数。
    ///   例如：1.234 存储为 1234，2.5 存储为 2500。
    ///   加减法直接对 Raw 进行整数加减即可（因为放大倍数相同）。
    ///   乘法需要额外除以 QUANTA（因为两个放大后的数相乘，结果被放大了 QUANTA² 倍，需要除回来）。
    ///   除法需要额外乘以 QUANTA（因为两个放大后的数相除，放大倍数被抵消了，需要乘回来）。
    ///
    /// 使用场景：
    ///   - 所有游戏逻辑中的数值计算（位置、速度、伤害等）
    ///   - 任何需要跨机器确定性一致的计算
    ///   - 不适用于：渲染显示（需先调用 ToFloat 转换）、用户输入（需先调用 FromFloat 转换）
    /// </summary>
    public readonly struct FixedPoint
    {
        // 内部使用 1000 倍放大，等同于"3 位定点小数"。
        // 选择 1000 的原因：简单直观，3 位精度足以满足大多数移动逻辑；
        // 更高精度可用 10000（4 位小数）或 2 的幂次（便于位运算优化）。
        /// <summary>
        /// 定点数的放大因子（量子单位）。QUANTA = 1000 表示保留 3 位小数精度。
        /// 例如：实际值 1.234 在内部存储为 Raw = 1234。
        /// </summary>
        public const long QUANTA = 1000L;

        // 实际存储的"放大后的整数"。例如 1.234 -> Raw = 1234。
        /// <summary>
        /// 放大后的原始整数值。这是定点数的内部存储形式。
        /// 实际数值 = Raw / QUANTA。
        /// 例如：Raw = 1234 代表实际值 1.234，Raw = -5000 代表实际值 -5.0。
        /// </summary>
        public readonly long Raw;

        /// <summary>
        /// 使用原始放大整数值构造定点数。
        /// 注意：此构造函数的参数是已经放大 QUANTA 倍的整数值，不是实际数值。
        /// 如果要从实际浮点数值构造，请使用 <see cref="FromFloat"/> 方法。
        /// </summary>
        /// <param name="raw">已放大 QUANTA 倍的整数值。例如要表示 1.5，应传入 1500。</param>
        public FixedPoint(long raw)
        {
            Raw = raw;
        }

        // 用 float 创建定点数（仅在"输入边界"使用一次，之后一律走整数运算）。
        /// <summary>
        /// 从 float 浮点数创建定点数。这是将浮点数引入定点数系统的"入口"方法。
        ///
        /// 重要：此方法仅在"输入边界"（即用户输入/配置读取等需要从浮点转定点的位置）调用一次，
        /// 之后所有运算都通过定点数的整数运算完成，不再涉及浮点，从而保证确定性。
        ///
        /// 为什么用 MathF.Round 而不是直接强转？
        ///   直接 (long) 强转会截断（向零取整），对负数会产生方向偏差：
        ///   例如 (long)(-0.6 * 1000) = -599 而非 -600。
        ///   MathF.Round 四舍五入可以避免这个问题，使正负数的舍入行为对称。
        /// </summary>
        /// <param name="f">要转换的浮点数值。例如 1.234f 会被转换为 Raw=1234 的定点数。</param>
        /// <returns>对应的定点数实例。</returns>
        public static FixedPoint FromFloat(float f)
        {
            // 用 Math.Round 避免负数截断方向问题；这是输入边界处允许的浮点使用。
            return new FixedPoint((long)MathF.Round(f * QUANTA));
        }

        // 转回 float 仅用于"显示/渲染"，不参与逻辑推进。
        /// <summary>
        /// 将定点数转换回 float 浮点数，用于显示和渲染。
        ///
        /// 重要：此方法仅用于"输出边界"（即将定点数结果显示给玩家或传给渲染引擎），
        /// 绝对不要在游戏逻辑中使用此方法参与计算，否则会破坏确定性！
        /// </summary>
        /// <returns>对应的浮点数值。精度最多保留 3 位小数。</returns>
        public float ToFloat()
        {
            return Raw / (float)QUANTA;
        }

        // ====== 运算符重载：所有运算都使用整数，保证确定性。 ======
        // 加减法：因为两个数的 Raw 已经同比例放大，直接加减即可，无需额外处理。
        // 乘法：Raw(a) * Raw(b) = 实际值(a) * QUANTA * 实际值(b) * QUANTA
        //       = 实际值(a*b) * QUANTA²，所以需要除以 QUANTA 还原为 QUANTA 倍。
        // 除法：Raw(a) / Raw(b) = (实际值(a) * QUANTA) / (实际值(b) * QUANTA)
        //       = 实际值(a/b)，放大倍数被抵消，所以需要乘以 QUANTA 恢复 QUANTA 倍。

        /// <summary>
        /// 定点数加法。直接对 Raw 整数相加，因为放大倍数相同。
        /// </summary>
        public static FixedPoint operator +(FixedPoint a, FixedPoint b) => new FixedPoint(a.Raw + b.Raw);

        /// <summary>
        /// 定点数减法。直接对 Raw 整数相减，因为放大倍数相同。
        /// </summary>
        public static FixedPoint operator -(FixedPoint a, FixedPoint b) => new FixedPoint(a.Raw - b.Raw);

        /// <summary>
        /// 定点数乘法。两个 Raw 相乘后结果被放大了 QUANTA² 倍，需要除以 QUANTA 还原。
        /// 例如：(1.5 * 2.0 = 3.0) → Raw: (1500 * 2000 / 1000 = 3000) → 实际值 3.0 ✓
        /// </summary>
        public static FixedPoint operator *(FixedPoint a, FixedPoint b) => new FixedPoint(a.Raw * b.Raw / QUANTA);

        /// <summary>
        /// 定点数除法。两个 Raw 相除后放大倍数被抵消，需要乘以 QUANTA 恢复。
        /// 先乘 QUANTA 再除，可以避免整数除法截断导致的精度损失。
        /// 例如：(3.0 / 2.0 = 1.5) → Raw: (3000 * 1000 / 2000 = 1500) → 实际值 1.5 ✓
        ///
        /// 注意：除以零会产生除零异常，与普通整数除法行为一致，调用方需自行保证除数非零。
        /// </summary>
        public static FixedPoint operator /(FixedPoint a, FixedPoint b) => new FixedPoint(a.Raw * QUANTA / b.Raw);

        /// <summary>判断两个定点数是否相等。直接比较 Raw 整数值。</summary>
        public static bool operator ==(FixedPoint a, FixedPoint b) => a.Raw == b.Raw;
        /// <summary>判断两个定点数是否不等。</summary>
        public static bool operator !=(FixedPoint a, FixedPoint b) => a.Raw != b.Raw;
        /// <summary>判断 a 是否大于 b。</summary>
        public static bool operator >(FixedPoint a, FixedPoint b) => a.Raw > b.Raw;
        /// <summary>判断 a 是否小于 b。</summary>
        public static bool operator <(FixedPoint a, FixedPoint b) => a.Raw < b.Raw;
        /// <summary>判断 a 是否大于等于 b。</summary>
        public static bool operator >=(FixedPoint a, FixedPoint b) => a.Raw >= b.Raw;
        /// <summary>判断 a 是否小于等于 b。</summary>
        public static bool operator <=(FixedPoint a, FixedPoint b) => a.Raw <= b.Raw;

        // 常量：0 与 1 在帧同步里非常常用，做静态缓存避免重复构造。
        /// <summary>
        /// 定点数零值。Raw = 0，代表实际值 0.0。
        /// 常用于初始化位置、速度等归零操作。
        /// </summary>
        public static readonly FixedPoint Zero = new FixedPoint(0);

        /// <summary>
        /// 定点数一值。Raw = QUANTA (1000)，代表实际值 1.0。
        /// 常用于归一化方向、系数为 1 的乘法等。
        /// </summary>
        public static readonly FixedPoint One = new FixedPoint(QUANTA);

        /// <summary>
        /// 判断此定点数是否与另一个对象相等。
        /// 仅当对方也是 FixedPoint 且 Raw 值相同时返回 true。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>如果 obj 是 FixedPoint 且 Raw 值相同，返回 true；否则返回 false。</returns>
        public override bool Equals(object obj) => obj is FixedPoint other && other.Raw == Raw;

        /// <summary>
        /// 获取此定点数的哈希码。直接使用 Raw 的哈希码。
        /// </summary>
        /// <returns>Raw 值的哈希码。</returns>
        public override int GetHashCode() => Raw.GetHashCode();

        /// <summary>
        /// 将定点数转换为字符串表示，保留 3 位小数。
        /// 例如 Raw=1234 会输出 "1.234"。主要用于调试和日志输出。
        /// </summary>
        /// <returns>格式化后的字符串，保留 3 位小数。</returns>
        public override string ToString() => ToFloat().ToString("F3");
    }
}
