using System;
using System.Collections.Generic;
using MemoryPack;

namespace LockstepShared
{
    /// <summary>
    /// 玩家单帧输入指令。
    ///
    /// 这个结构是"帧同步里真正被同步的东西"——我们不同步血量、位置，只同步输入。
    /// 每个玩家在同一帧里，给到同样的输入集合，确定性模拟就必然演化出完全相同的结果。
    ///
    /// 使用 byte 位标志 (bit flags) 是为了节省带宽，每帧每玩家仅 1 字节即可表达方向与开火。
    ///
    /// 设计思路：
    ///   帧同步的核心原则是"只同步输入，不同步状态"。之所以这样设计，是因为：
    ///   1. 输入数据量远小于状态数据量（1 字节 vs 可能数百字节的状态）
    ///   2. 只要模拟是确定性的，同样的输入必然产生同样的状态，无需传输状态
    ///   3. 用位标志 (bit flags) 压缩方向信息，4 个方向只需 4 bit，加上开火标志总共不到 1 字节
    ///
    /// 使用场景：
    ///   - 客户端每帧采集键盘输入后构造 PlayerInput 并发送给服务端
    ///   - 服务端收集所有玩家的 PlayerInput 后打包成 FrameData 广播
    ///   - 客户端收到 FrameData 后用其中的 PlayerInput 推进本地模拟
    /// </summary>
    [Serializable]
    [MemoryPackable]
    public partial class PlayerInput
    {
        // ============ 成员字段 ============

        /// <summary>
        /// 无参构造函数，供 MemoryPack 反序列化使用。
        /// </summary>
        [MemoryPackConstructor]
        public PlayerInput() { }

        /// <summary>
        /// 玩家 Id。服务端用它把输入归位到正确玩家。
        /// 每个玩家在房间内有唯一的 Id，通常由服务端在 Welcome 消息中分配。
        /// </summary>
        public int PlayerId;

        /// <summary>
        /// 这个输入属于哪一帧。服务端用来把它收进对应的 FrameData。
        /// Tick 从 0 开始递增，每个逻辑帧 +1。
        /// </summary>
        public int Tick;

        /// <summary>
        /// 方向位：bit0=下 bit1=上 bit2=左 bit3=右。多 bit 同时置 1 表示组合方向（如左上）。
        ///
        /// 位分配说明（从低位到高位）：
        ///   bit0 (值=1): 下  → 对应 S 键 / DownArrow
        ///   bit1 (值=2): 上  → 对应 W 键 / UpArrow
        ///   bit2 (值=4): 左  → 对应 A 键 / LeftArrow
        ///   bit3 (值=8): 右  → 对应 D 键 / RightArrow
        ///
        /// 例如：同时按 W+D（右上）时 MoveDir = 2|8 = 10。
        ///
        /// ★ 这个位分配与键盘采集端 (Program.KeyboardLoop / UnityBridge) 完全一致：
        /// W/Up→bit1(上)、S/Down→bit0(下)、A/Left→bit2(左)、D/Right→bit3(右)。
        /// 如果修改了这里的位分配，必须同步修改输入采集端，否则会出现"按上却向下"的静默 bug。
        /// 
        /// 注意：使用 int 而非 byte，因为 UnityEngine.JsonUtility 不支持 byte 字段序列化。
        /// </summary>
        public int MoveDir;

        /// <summary>
        /// 开火位：bit0=是否本帧开火。本示例未实现命中检测，仅演示"指令传递链路"。
        /// 这是一个 bool 而非 bit 标志，因为只有 1 个动作，序列化开销可忽略。
        /// </summary>
        public bool Fire;

        // ============ 便捷构造 ============

        /// <summary>
        /// 提供给协议层用的"本地输入"快捷构造。常见于客户端把键盘按键打包成指令。
        /// </summary>
        /// <param name="playerId">本玩家的 Id，由服务端在 Welcome 消息中分配。</param>
        /// <param name="tick">当前帧号，由客户端的 TickTimer 或帧计数器提供。</param>
        /// <param name="moveDir">方向位标志，通过位运算组合方向。例如：同时按上和右时 moveDir = 2|8 = 10。</param>
        /// <param name="fire">本帧是否按下开火键。</param>
        public PlayerInput(int playerId, int tick, int moveDir, bool fire)
        {
            PlayerId = playerId;
            Tick = tick;
            MoveDir = moveDir;
            Fire = fire;
        }

        /// <summary>
        /// 空输入工厂方法：玩家断线/没操作时用它兜底，保证服务端仍能产帧。
        ///
        /// 为什么需要空输入？
        ///   帧同步要求"每一帧所有玩家都有输入"，否则模拟无法推进。
        ///   当某个玩家断线或卡顿时，服务端会用空输入 (MoveDir=0, Fire=false) 代替，
        ///   这样该玩家在本帧相当于"站着不动"，不会卡住其他所有人。
        /// </summary>
        /// <param name="playerId">缺输入的玩家 Id。</param>
        /// <param name="tick">需要补输入的帧号。</param>
        /// <returns>一个 MoveDir=0、Fire=false 的空 PlayerInput 实例。</returns>
        public static PlayerInput Empty(int playerId, int tick) =>
            new PlayerInput(playerId, tick, 0, false);

        // ============ 方向位的便捷访问器（避免到处写位运算） ============
        // ★ bit 分配必须与键盘采集端一致，否则会出现"按上却向下/方向错乱"的静默 bug。
        // 这些属性都由 MoveDir 派生，加 [JsonIgnore] 不参与网络序列化（避免与 MoveDir 冗余传输）。
        //
        // 位运算原理：
        //   读取：用 AND(&) 检查某位是否为 1。例如 (MoveDir & 2) != 0 检查 bit1 是否置位。
        //   设置：用 OR(|) 将某位置 1。例如 MoveDir |= 2 将 bit1 设为 1。
        //   清除：用 AND(&) 配合取反(~)将某位清 0。例如 MoveDir &= 0xFD 将 bit1 清 0（0xFD = ~0x02）。

        /// <summary>
        /// 上方向（bit1，值=2）。对应 W 键 / UpArrow。
        /// 读取时检查 bit1 是否为 1；设置时将 bit1 置 1，清除时将 bit1 置 0。
        /// </summary>
        [MemoryPackIgnore] public bool Up    { get { return (MoveDir & 2) != 0; } set { if (value) MoveDir |= 2; else MoveDir &= 0xFD; } }

        /// <summary>
        /// 下方向（bit0，值=1）。对应 S 键 / DownArrow。
        /// 读取时检查 bit0 是否为 1；设置时将 bit0 置 1，清除时将 bit0 置 0。
        /// </summary>
        [MemoryPackIgnore] public bool Down  { get { return (MoveDir & 1) != 0; } set { if (value) MoveDir |= 1; else MoveDir &= 0xFE; } }

        /// <summary>
        /// 左方向（bit2，值=4）。对应 A 键 / LeftArrow。
        /// 读取时检查 bit2 是否为 1；设置时将 bit2 置 1，清除时将 bit2 置 0。
        /// </summary>
        [MemoryPackIgnore] public bool Left  { get { return (MoveDir & 4) != 0; } set { if (value) MoveDir |= 4; else MoveDir &= 0xFB; } }

        /// <summary>
        /// 右方向（bit3，值=8）。对应 D 键 / RightArrow。
        /// 读取时检查 bit3 是否为 1；设置时将 bit3 置 1，清除时将 bit3 置 0。
        /// </summary>
        [MemoryPackIgnore] public bool Right { get { return (MoveDir & 8) != 0; } set { if (value) MoveDir |= 8; else MoveDir &= 0xF7; } }
    }

    /// <summary>
    /// 某一逻辑帧内"所有玩家输入"的集合。
    ///
    /// 这个结构会被服务端广播给所有客户端，客户端拿到它就能推进一帧世界逻辑。
    ///
    /// 数据流：
    ///   客户端发送 PlayerInput → 服务端收集同一 tick 的所有 PlayerInput →
    ///   打包成 FrameData → 广播给所有客户端 → 客户端用 Simulation.Step(frame) 推进逻辑
    ///
    /// 为什么要把所有玩家的输入打包在一起？
    ///   因为 Simulation.Step 需要同时处理所有玩家的输入才能正确推进一帧。
    ///   如果只收到部分玩家的输入就推进，不同客户端的处理顺序可能不同，导致不同步。
    /// </summary>
    [Serializable]
    [MemoryPackable]
    public partial class FrameData
    {
        /// <summary>
        /// 无参构造函数，供 MemoryPack 反序列化使用。
        /// </summary>
        [MemoryPackConstructor]
        public FrameData() { }

        /// <summary>
        /// 本帧的 tick 序号。客户端用它对齐 tick（缺失的 tick 必须补空帧推进）。
        /// tick 从 0 开始递增，每次 Simulation.Step 后 +1。
        /// </summary>
        public int Tick;

        /// <summary>
        /// 本帧所有玩家的输入列表（长度 = 房间内玩家数）。
        /// 每个元素对应一个玩家的输入，通过 PlayerInput.PlayerId 区分。
        /// </summary>
        public List<PlayerInput> Inputs = new List<PlayerInput>();

        /// <summary>
        /// 构造指定 tick 的帧数据容器。
        /// </summary>
        /// <param name="tick">本帧的 tick 序号。</param>
        public FrameData(int tick)
        {
            Tick = tick;
        }
    }
}
