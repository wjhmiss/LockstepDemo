using System.Collections.Generic;

namespace LockstepShared
{
    /// <summary>
    /// 玩家实体。所有状态都使用 FixedPoint 表示，确保两边演化结果完全一致。
    ///
    /// 设计说明：
    ///   在帧同步架构中，"实体"是指游戏世界中的逻辑对象。
    ///   每个实体只存储"状态"（位置、方向等），不存储"行为"（行为由 Simulation 统一处理）。
    ///   这与 ECS (Entity-Component-System) 架构的思想一致：数据与逻辑分离。
    ///
    ///   为什么所有数值都用 FixedPoint 而不是 float？
    ///   因为 float 在不同 CPU 上的运算结果可能有微小差异，
    ///   而帧同步要求所有客户端的状态 byte-for-byte 一致，
    ///   所以必须使用确定性的定点数。
    /// </summary>
    public class PlayerEntity
    {
        /// <summary>
        /// 玩家唯一标识。由服务端分配，用于在 WorldState.Players 字典中索引。
        /// </summary>
        public int Id;

        /// <summary>
        /// 玩家在地图上的 X 坐标（定点数）。
        /// 渲染层会调用 X.ToFloat() 转回 float 交给 Unity 渲染引擎使用。
        /// 坐标系：X 向右为正，原点在左下角。
        /// </summary>
        public FixedPoint X;

        /// <summary>
        /// 玩家在地图上的 Y 坐标（定点数）。
        /// 坐标系：Y 向上为正（数学坐标系），原点在左下角。
        /// </summary>
        public FixedPoint Y;

        /// <summary>
        /// 上一次移动方向，存储原始 MoveDir 字节。
        /// 可用于美术朝向（如角色精灵翻转）、动画选择等。
        /// 值为 0 表示玩家没有移动过或在上一帧没有按方向键。
        /// </summary>
        public byte LastMoveDir;

        /// <summary>
        /// 本帧是否开火，给渲染层做枪口闪光 (muzzle flash) 等特效。
        /// 每帧结束后由 Simulation.Step 重新赋值，只有一帧的时效。
        /// </summary>
        public bool FiredThisFrame;

        /// <summary>
        /// 构造玩家实体，指定玩家 Id。初始位置为 (0, 0)。
        /// </summary>
        /// <param name="id">玩家唯一标识。</param>
        public PlayerEntity(int id)
        {
            Id = id;
            X = FixedPoint.Zero;
            Y = FixedPoint.Zero;
        }
    }

    /// <summary>
    /// 世界状态——"整个游戏当前的全量状态"。
    ///
    /// 核心概念：
    ///   WorldState 是一帧逻辑推进后的"快照"。它包含该 tick 下所有玩家的完整信息。
    ///   在帧同步中，我们从不直接通过网络传输 WorldState（太大了），
    ///   而是通过传输 PlayerInput 让每台机器自己推算出 WorldState。
    ///   如果两台机器的 WorldState 一致，说明帧同步是正确的。
    ///
    /// 快照哈希 (ComputeHash) 的作用：
    ///   用一个 HashCode 表示整个 WorldState，是预测回滚里"判断两份状态是否一致"
    ///   最廉价、最典型的做法。服务端可以定期广播哈希值让客户端校验同步是否正确。
    ///
    /// 快照克隆 (CloneSnapshot) 的作用：
    ///   在客户端预测回滚中，客户端需要"先保存当前状态，预测性推进几帧，
    ///   收到服务端权威帧后发现不一致，就回滚到保存的状态重新模拟"。
    ///   CloneSnapshot 就是用来保存那个"回滚点"的。
    /// </summary>
    public class WorldState
    {
        /// <summary>
        /// tick：表示这份状态是推进到第几帧之后的结果。
        /// 例如 Tick=5 表示这是经过 5 次 Simulation.Step 后的状态。
        /// 客户端用此值判断是否缺少帧（如果收到的权威帧 tick 跳跃了，中间的帧需要补推）。
        /// </summary>
        public int Tick;

        /// <summary>
        /// 玩家集合，以 PlayerId 为 key 索引。这样保证遍历顺序稳定。
        ///
        /// 为什么用 Dictionary 而不是 List？
        ///   1. 通过 PlayerId 快速查找玩家实体（O(1)），Simulation.Step 中频繁使用
        ///   2. 保证遍历顺序由插入顺序决定（.NET 中 Dictionary 保持插入顺序），
        ///      这对 ComputeHash 的确定性很重要
        ///   3. 防止同一 PlayerId 被重复添加
        /// </summary>
        public Dictionary<int, PlayerEntity> Players = new Dictionary<int, PlayerEntity>();

        /// <summary>
        /// 添加一个玩家到世界中。构造时给定 playerId，所有玩家初始坐标都是 (0,0)。
        ///
        /// 如果该 playerId 已存在，则不会重复添加（幂等操作）。
        /// </summary>
        /// <param name="playerId">要添加的玩家唯一标识。</param>
        public void AddPlayer(int playerId)
        {
            // 幂等检查：避免重复添加同一个玩家。如果已存在则跳过。
            if (!Players.ContainsKey(playerId))
            {
                Players[playerId] = new PlayerEntity(playerId);
            }
        }

        /// <summary>
        /// 计算世界状态的"快照哈希"。
        ///
        /// 用途：
        ///   服务端产帧/客户端回滚对账时用它判断"两边是否同步一致"。
        ///   如果两端同一 tick 的哈希值相等，基本可以确定状态一致。
        ///
        /// 算法原理：
        ///   采用异或 (XOR) + 质数乘法的方式生成哈希值：
        ///   1. 用 Tick 乘以质数 73856093 作为初始哈希（将 tick 值"分散"到更大的数值空间）
        ///   2. 对每个玩家，将其 Id、X.Raw、Y.Raw 分别乘以不同质数后异或到哈希中
        ///   3. 质数乘法的作用是"混淆"——防止不同字段值相同时产生相同哈希
        ///   4. 异或操作保证字段的计算顺序可以任意交换（交换律）
        ///
        /// 备注给新手：
        ///   真实打比赛级的代码，会基于 state 生成一个"前缀无关"的哈希，
        ///   用 xxHash 之类速度快的算法，并对每个玩家的字段排序后写入。
        ///   本示例用简单拼接的 GoalHash，已经足够教学。
        ///   它最宽松，如果它相等，基本可判定状态一致。
        ///
        ///   碰撞风险：由于使用简单异或哈希，理论上存在碰撞可能（不同状态产生相同哈希），
        ///   但在教学场景下概率极低，可以忽略。
        /// </summary>
        /// <returns>一个 long 类型的哈希值，可用于快速比较两个 WorldState 是否一致。</returns>
        public long ComputeHash()
        {
            // 用 long 累加每个玩家的原始坐标，做最易理解的"拼接哈希"。
            // 初始值用 Tick 乘以质数，使不同 tick 的状态天然不同。
            long h = Tick * 73856093L;
            foreach (var kv in Players)
            {
                // 哈希顺序恒定：玩家字典遍历顺序由插入顺序决定，本示例保证房间内玩家先插入先收。
                // 严格生产环境会对 key 排序后再哈希，以确保不同运行环境下遍历顺序一致。
                var p = kv.Value;
                // 每个字段乘以不同质数后异或，目的是：
                // 1. 质数乘法使相邻值（如 Id=1 和 Id=2）的哈希差异更大（"雪崩效应"）
                // 2. 不同字段用不同质数，防止字段值相同时异或结果抵消为 0
                h ^= (long)p.Id * 83492791L;    // 玩家 Id 乘以质数 83492791
                h ^= p.X.Raw * 19349663L;        // X 坐标乘以质数 19349663（与 Id 的质数不同）
                h ^= p.Y.Raw * 83492791L;        // Y 坐标乘以质数 83492791
            }
            return h;
        }

        /// <summary>
        /// 显式快照：克隆一份独立副本，用于回滚前的保存。
        ///
        /// 为什么需要深拷贝？
        ///   在客户端预测回滚中，客户端会"预测性"地推进几帧。
        ///   如果之后收到服务端的权威帧发现与预测不一致，就需要"回滚"——
        ///   恢复到预测前的状态，然后用正确的输入重新模拟。
        ///   如果只保存引用（浅拷贝），回滚时修改的就是同一个对象，无法恢复原状态。
        ///
        /// 为什么不用 MemberwiseClone？
        ///   MemberwiseClone 是浅拷贝，Players 字典中的 PlayerEntity 对象仍然是引用共享。
        ///   修改克隆后的玩家坐标会影响原始快照，导致回滚失效。
        /// </summary>
        /// <returns>一份完全独立的 WorldState 副本，修改副本不会影响原对象。</returns>
        public WorldState CloneSnapshot()
        {
            var clone = new WorldState();
            clone.Tick = Tick;  // 复制 tick 序号（值类型，直接复制即可）
            foreach (var kv in Players)
            {
                // 对每个玩家实体做深拷贝：创建新的 PlayerEntity 对象并逐字段复制。
                var src = kv.Value;
                var dst = new PlayerEntity(src.Id)
                {
                    X = src.X,                      // FixedPoint 是 readonly struct，赋值即深拷贝
                    Y = src.Y,                      // 同上
                    LastMoveDir = src.LastMoveDir,   // byte 是值类型，直接复制
                    FiredThisFrame = src.FiredThisFrame, // bool 是值类型，直接复制
                };
                clone.Players[src.Id] = dst;
            }
            return clone;
        }
    }
}
