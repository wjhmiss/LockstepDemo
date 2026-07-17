using System;
using System.Collections.Generic;
using MemoryPack;

namespace LockstepShared
{
    // === 上行消息（Client → Server）===

    /// <summary>
    /// 客户端 → 服务端：加入房间请求。
    /// </summary>
    [Serializable]
    [MemoryPackable]
    public partial class HelloMessage
    {
        public string Type;
        public string Name;
    }

    /// <summary>
    /// 客户端 → 服务端：上报本帧输入。
    /// </summary>
    [Serializable]
    [MemoryPackable]
    public partial class InputMessage
    {
        public string Type;
        public int Tick;
        public int MoveDir;
        public bool Fire;
    }

    /// <summary>
    /// 双向：玩家退出通知。
    /// </summary>
    [Serializable]
    [MemoryPackable]
    public partial class GoodbyeMessage
    {
        public string Type;
        public int PlayerId;
    }

    // === 下行消息（Server → Client）===

    /// <summary>
    /// 服务端 → 客户端：欢迎消息，包含分配的 PlayerId 和当前房间内已有玩家列表。
    /// </summary>
    [Serializable]
    [MemoryPackable]
    public partial class WelcomeMessage
    {
        public string Type;
        public int PlayerId;
        public int ServerTick;
        public List<int> ExistingPlayers;
    }

    /// <summary>
    /// 服务端 → 客户端：广播权威帧数据。
    /// </summary>
    [Serializable]
    [MemoryPackable]
    public partial class FrameMessage
    {
        public string Type;
        public FrameDataWrapper Frame;
    }

    /// <summary>
    /// FrameData 的序列化包装，用于网络传输。
    /// </summary>
    [Serializable]
    [MemoryPackable]
    public partial class FrameDataWrapper
    {
        public int Tick;
        public List<PlayerInput> Inputs;
    }

    /// <summary>
    /// 服务端 → 客户端：新玩家加入通知。
    /// </summary>
    [Serializable]
    [MemoryPackable]
    public partial class PlayerJoinMessage
    {
        public string Type;
        public int PlayerId;
    }
}
