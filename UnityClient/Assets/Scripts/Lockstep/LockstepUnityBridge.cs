using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using MemoryPack;
using LockstepShared;
using Lockstep.Client;
// Resolve ambiguity between LockstepShared.PlayerInput and UnityEngine.InputSystem.PlayerInput
using PlayerInput = LockstepShared.PlayerInput;

namespace Lockstep.Unity
{
    /// <summary>
    /// 在 Unity 中驱动帧同步的桥接 MonoBehaviour（桥接器）。
    /// <para>
    /// 这个脚本的职责非常薄，仅充当"Unity 引擎 ↔ 帧同步内核"之间的桥梁：
    ///   1) 启动时（Start）：创建 NetClient + LockstepRunner，注册回调，并发起 Hello 加入房间；
    ///   2) 每帧更新（Update）：采集本地键盘输入（W/A/S/D 与空格），喂给 LockstepRunner，并调用 Update() 推进帧同步；
    ///   3) 渲染同步：把世界中的玩家实体映射成场景中的 Cube（一个 per-Player 的 GameObject 池），
    ///      权威世界用红色，预测世界用绿色，方便对比观察。
    /// </para>
    /// <para>
    /// 使用方法：将此脚本挂载到场景中的任意 GameObject 上，然后在 Inspector 中配置服务端地址和端口。
    /// </para>
    /// </summary>
    public class LockstepUnityBridge : MonoBehaviour
    {
        /// <summary>
        /// 服务端 IP 地址。可在 Unity Inspector 中直接修改。
        /// 默认值 "127.0.0.1" 表示本机，适合本地开发测试。
        /// </summary>
        public string serverHost = "127.0.0.1";

        /// <summary>
        /// 服务端端口号。可在 Unity Inspector 中直接修改。
        /// 默认值 7777，需要与服务端监听端口一致。
        /// </summary>
        public int serverPort = 7777;

        /// <summary>
        /// 本玩家的名字。可在 Unity Inspector 中直接修改。
        /// 加入房间时会发送给服务端。
        /// </summary>
        public string playerName = "Player";

        /// <summary>
        /// 玩家视觉对象池：将 PlayerId 映射到场景中对应的 GameObject（Cube）。
        /// <para>
        /// key = 玩家 ID（权威世界的玩家加 10000 偏移，以区分权威/预测的视觉对象）；
        /// value = 代表该玩家的 Cube GameObject。
        /// 当世界状态更新时，直接移动已有的 Cube，避免每帧创建/销毁对象。
        /// </para>
        /// </summary>
        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();

        /// <summary>
        /// 网络消息线程安全缓冲队列。
        /// <para>
        /// NetClient 的接收循环在后台线程执行，直接调用 OnNetMessage 会与
        /// Unity 主线程的 Update/ClientTick 并发访问 LockstepRunner 内部的
        /// Queue 和 Dictionary，导致帧丢失或状态损坏。
        /// 这里把后台线程收到的原始消息入队，在 Update 开头于主线程统一处理。
        /// </para>
        /// </summary>
        private readonly Queue<(byte kind, byte[] payload)> _pendingNetMessages = new Queue<(byte, byte[])>();
        private readonly object _netMsgLock = new object();

        /// <summary>
        /// 网络客户端对象。负责与服务端的 TCP 通信。
        /// </summary>
        private NetClient _net;

        /// <summary>
        /// 帧同步核心对象。负责管理权威世界、预测世界和回滚对账。
        /// </summary>
        private LockstepRunner _runner;

        /// <summary>
        /// 世界坐标到 Unity 场景坐标的缩放因子。
        /// </summary>
        public float cell = 1.0f;

        /// <summary>
        /// Unity 生命周期：脚本启动时调用。
        /// </summary>
        void Start()
        {
            // 创建帧同步核心对象和网络客户端
            _runner = new LockstepRunner();
            _net = new NetClient(serverHost, serverPort);

            // 注册网络消息回调：收到服务端消息时调用 OnNetMessage 处理
            _net.OnMessage = OnNetMessage;

            // 加入房间：向服务端发送 Hello 消息，告知玩家名字
            _net.Send(new HelloMessage { Type = MessageType.Hello, Name = playerName });

            // ★ 把"本地输入发给服务端"这条链路接起来。
            //   LockstepRunner 每 tick 产生一份输入，通过此 lambda 转交给 NetClient 实际发送。
            _runner.OnSendInput = input =>
            {
                _net.Send(new InputMessage
                {
                    Type = MessageType.Input,
                    Tick = input.Tick,
                    MoveDir = (int)input.MoveDir,
                    Fire = input.Fire,
                });
            };

            // 钩子：每权威 tick 后把世界同步给 Unity 视觉
            // _runner.OnAuthoritativeTick = w => ApplyWorld(w, authoritative: true);
            _runner.OnPredictedTick      = w => ApplyWorld(w, authoritative: false);
        }

        /// <summary>
        /// Unity 生命周期：每帧调用。采集键盘输入并驱动帧同步逻辑。
        /// </summary>
        void Update()
        {
            if (_runner == null) return;

            // 0) 在主线程上处理后台线程收到的网络消息
            ProcessPendingNetMessages();

            // 1) 采集键盘输入（Input System）
            byte dir = 0;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    dir |= 2;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  dir |= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)   dir |= 4;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)  dir |= 8;
                _runner.CurrentMoveDir = dir;
                _runner.CurrentFire = kb.spaceKey.isPressed;
            }

            // 2) 推进帧同步逻辑
            _runner.Update();

            // 3) 渲染预测世界
            ApplyWorld(_runner.PredictedWorld, authoritative: false);
        }

        /// <summary>
        /// Unity 生命周期：应用退出时调用。向服务端发送 Goodbye 消息。
        /// </summary>
        void OnApplicationQuit()
        {
            _net?.Send(new GoodbyeMessage { Type = MessageType.Goodbye, PlayerId = _runner.MyPlayerId });
        }

        /// <summary>
        /// 网络消息回调：收到服务端消息时由 NetClient 的后台接收线程调用。
        /// <para>
        /// 此方法运行在后台线程，不能直接操作 LockstepRunner 的非线程安全状态，
        /// 仅将消息入队，由 Update 中的 ProcessPendingNetMessages 在主线程处理。
        /// </para>
        /// </summary>
        /// <param name="kind">消息类型字节</param>
        /// <param name="payload">MemoryPack 二进制载荷</param>
        void OnNetMessage(byte kind, byte[] payload)
        {
            lock (_netMsgLock)
            {
                _pendingNetMessages.Enqueue((kind, payload));
            }
        }

        /// <summary>
        /// 在主线程上处理所有待处理的网络消息。
        /// <para>
        /// 从缓冲队列中取出后台线程收到的消息，按类型反序列化并分发给 LockstepRunner。
        /// 由于此处运行在 Unity 主线程，与 ClientTick 串行执行，不会有并发问题。
        /// </para>
        /// </summary>
        void ProcessPendingNetMessages()
        {
            while (true)
            {
                byte kind;
                byte[] payload;

                lock (_netMsgLock)
                {
                    if (_pendingNetMessages.Count == 0)
                        return;
                    (kind, payload) = _pendingNetMessages.Dequeue();
                }

                switch (kind)
                {
                    case MessageType.MsgWelcome:
                    {
                        var msg = MemoryPackSerializer.Deserialize<WelcomeMessage>(payload);
                        _runner.MyPlayerId = msg.PlayerId;

                        var existing = new List<int>(msg.ExistingPlayers);
                        existing.Add(msg.PlayerId);
                        _runner.InitPlayers(existing);
                        _runner.InitFromServerTick(msg.ServerTick);
                        Debug.Log($"[Lockstep] Welcome: PlayerId={msg.PlayerId}, ServerTick={msg.ServerTick}, Players={existing.Count}");
                        break;
                    }

                    case MessageType.MsgFrame:
                    {
                        var msg = MemoryPackSerializer.Deserialize<FrameMessage>(payload);
                        var frame = new FrameData(msg.Frame.Tick);
                        foreach (var inp in msg.Frame.Inputs)
                        {
                            frame.Inputs.Add(new PlayerInput(inp.PlayerId, inp.Tick, inp.MoveDir, inp.Fire));
                        }
                        _runner.OnReceiveServerFrame(frame);
                        break;
                    }

                    case MessageType.MsgGoodbye:
                    {
                        var msg = MemoryPackSerializer.Deserialize<GoodbyeMessage>(payload);
                        if (_views.TryGetValue(msg.PlayerId, out var go))
                            Destroy(go);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 将世界状态映射到 Unity 场景中的视觉对象。
        /// <para>
        /// 预测世界：脚本挂载的 GameObject 自身即为本地玩家的视觉对象，
        ///   直接移动 this.transform 到预测位置，无需新建对象。
        /// 权威世界：为每个玩家创建独立的红色 Cube 用于对比观察。
        /// </para>
        /// </summary>
        void ApplyWorld(WorldState w, bool authoritative)
        {
            foreach (var kv in w.Players)
            {
                var p = kv.Value;

                if (authoritative)
                {
                    // 权威世界：为每个玩家创建/更新红色 Cube（用于对比观察）
                    var key = 10000 + p.Id;
                    if (!_views.TryGetValue(key, out var authCube))
                    {
                        authCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        authCube.name = "Auth_" + p.Id;
                        authCube.GetComponent<MeshRenderer>().material.color = Color.red;
                        _views[key] = authCube;
                    }
                    authCube.transform.position = new Vector3(p.X.ToFloat() * cell, 0, p.Y.ToFloat() * cell);
                }
                else
                {
                    // 预测世界：脚本挂载的 GameObject 自身即为本地玩家
                    if (p.Id == _runner.MyPlayerId)
                    {
                        transform.position = new Vector3(p.X.ToFloat() * cell, 0, p.Y.ToFloat() * cell);
                    }
                    else
                    {
                        // 其他玩家：用绿色 Cube 显示
                        var key = p.Id;
                        if (!_views.TryGetValue(key, out var otherCube))
                        {
                            otherCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            otherCube.name = "Pred_" + p.Id;
                            otherCube.GetComponent<MeshRenderer>().material.color = Color.green;
                            _views[key] = otherCube;
                        }
                        otherCube.transform.position = new Vector3(p.X.ToFloat() * cell, 0, p.Y.ToFloat() * cell);
                    }
                }
            }
        }
    }
}
