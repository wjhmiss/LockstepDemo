# Unity / .NET Core 帧同步新手示例 (LockstepDemo)

> 面向**第一次接触帧同步**的同学：不用任何第三方库，纯 C# 写出"服务端 + 客户端 + 确定性模拟 + 预测回滚"，既能跑在 .NET Core 控制台里学习调试，又能整套搬到 Unity。

完成本示例后，你应该能回答下面这些问题：

1. 什么是"帧同步 (Lockstep)"？它和"状态同步"差在哪？
2. 为什么帧同步一定要"确定性 (Determinism)"？是怎么用代码保证的？
3. 服务端到底"同步"了什么，不同步什么？
4. 为什么客户端要做"预测 (Prediction)"和"回滚 (Rollback)"？怎么写？
5. 一份在 .NET Core 上能跑的帧同步代码，怎么"无损"地接到 Unity？

---

## 一、目录结构

```
LockstepDemo.sln              解决方案（含三个项目）
src/
├─ Lockstep.Shared/           ★ 共享内核（帧同步的核心：服务端/客户端两边都用同一份）
│  ├─ GlobalUsings.cs         全工程"隐式 using"
│  ├─ GameConfig.cs           全局常量：tick 率、地图、速度…
│  ├─ PlayerInput.cs          ★ 玩家单帧指令 + FrameData (一帧内所有玩家输入的集合)
│  ├─ WorldState.cs           世界状态：玩家位置 + 哈希/快照
│  ├─ Simulation.cs          ★ 确定性模拟：吃一帧输入推一帧世界（核心中的核心）
│  ├─ TickTimer.cs            稳定节拍器：避免 Sleep 抖动吃掉帧率
│  ├─ NetProtocol.cs          二进制网络协议：长度前缀 + UTF8 JSON
│  └─ FixedPoint.cs          ★ 定点数：用整数模拟小数，保证跨机确定性
│
├─ Lockstep.Server/           服务端：管理房间、收输入、产帧广播
│  ├─ GlobalUsings.cs
│  ├─ Program.cs              启动入口 + 房间 tick 线程
│  └─ Room.cs                 ★ Room 类：玩家会话 + 帧调度 + 广播
│
└─ Lockstep.Client/           客户端：键盘采集 + 网络收发 + 预测回滚 + 控制台渲染
   ├─ GlobalUsings.cs
   ├─ Program.cs              启动入口 + 键盘采集 + 控制台渲染
   ├─ NetClient.cs            阻塞读 TCP + 解包回调
   ├─ LockstepRunner.cs       ★ 客户端业务核心：权威世界 / 预测世界 / 输入缓存 / 回滚对账
   └─ Unity/
      └─ LockstepUnityBridge.cs   Unity 适配 MonoBehaviour（在 Unity 工程中挂到 GameObject 上）
```

带 `★` 的是"看完一定就能理解帧同步"的核心文件，其他文件是配套工程脚手架。

---

## 二、什么是帧同步？（3 分钟概念扫盲）

帧同步 (Lockstep / Deterministic Synchronous) 是一种**多人同步方案**：

| 维度 | 状态同步 (State Sync) | 帧同步 (Lockstep) |
|------|-----------------------|--------------------|
| 服务端下发的数据 | 每个实体的当前血量/位置/属性等**完整状态** | 仅**玩家输入指令** |
| 客户端怎么得到世界 | 直接读服务端给的状态 | 自己拿输入在本机跑一份**模拟** |
| 带宽 | 高（状态越多越贵） | 低（只有输入） |
| 一致性 | 服务端是绝对权威，客户端只渲染 | 客户端模拟出来的世界，可以与服务端 byte 级一致 |
| 为什么"反作弊/重放"友好 | 实现复杂 | 因为有了输入就能完整复盘整局，开图回放一行代码搞定 |

**帧同步的关键一切都在"输入"上：** 我们只把"按了哪些键"广播给所有人，让所有客户端自己跑模拟。于是：

- **跨网络同步的只有"输入"**，不同步位置、血量这些；
- 只要两边用**同一份模拟代码**、**同一份常数表**、**同一份初始状态**、**同样的输入序列**，演化的世界必然 byte 级一致；
- 这种"同样输入 → 同样输出"的性质，叫 **确定性 (Determinism)**，它是帧同步成立的数学前提。

---

## 三、确定性到底怎么回事？（重点，新手最容易忽略）

浮点 (float/double) 运算在不同 CPU 上结果微小不同，这种"1e-7 量级误差"在帧同步里会滚雪球，最后出现"我方看到你在 A 点，敌方看到你在 B 点"。所以帧同步的确定性要做三件事：

1. **整型代替浮点：** 用 [FixedPoint.cs](src/Lockstep.Shared/FixedPoint.cs)：内部用 `long` 存 `值×1000`，留 3 位小数。所有运算都是整数运算，跨机结果绝对一致。
2. **逻辑与渲染解耦：** `[Simulation].Step(frame)` 进入世界算的全部走 `FixedPoint`；**只有**渲染时调 `ToFloat()` 转回 `float` 喂给 Unity/控制台。
3. **避免一切"非确定性输入"：** 在 `Simulation.Step` 里 `禁止使用 DateTime.Now、UnityEngine.Random、float 运算、Guid 等任何"机/时相关"的东西`。我们用 `TickTimer` 只在外层渲染线程帮忙调度 tick 节拍，**永远不会**在 `Simulation.Step` 里读它。

---

## 四、实现流程（按数据流向，看一遍就懂）

本节是全文的"主干"。建议把它和源码并排打开：每读一小段，就回到对应文件验证。

### 4.1 全链路数据流向总览

一条"按键 → 落地成画面"的指令，在系统里要走完下面这条闭环（编号 ①~⑧ 即各环节）：

```
        客户端 A                                        服务端
 ┌──────────────────────┐                      ┌─────────────────────┐
 │  ① 采集键盘           │                      │                     │
 │  ② 打包成 PlayerInput │ === ③ TCP 上报 =====> │  ④ 收入 _pendingInputs │
 │  ⑦ 回滚对账 + 渲染    │                      │  ⑤ 每 tick 产 FrameData│
 │      (预测世界)       │ <== ⑥ TCP 广播 Frame= │     推进权威 Simulation│
 │  ⑧ 输出画面           │                      │                     │
 └──────────────────────┘                      └─────────────────────┘
```

- ①②③ 在客户端本地，**毫秒级**完成；
- ④⑤⑥ 在服务端，按 `GameConfig.ServerTickMs`(=50ms) 的固定节拍发生；
- ⑥ 要跨越一个网络往返 (RTT) 才能回到客户端，**这就是延迟的来源**，也是预测回滚存在的理由；
- ⑦⑧ 是客户端收到权威帧后的"对账 + 渲染"。

下面逐个环节展开。

---

### 4.2 客户端：从按键到上报（环节 ①②③）

**(1) 键盘采集（环节 ①）**

主循环直接用 Windows 原生 API `GetAsyncKeyState` 读取物理按键状态：

```csharp
byte dir = 0;
if (IsKeyDown(VK_W) || IsKeyDown(VK_UP))    dir |= 2;  // bit1 = 上
if (IsKeyDown(VK_S) || IsKeyDown(VK_DOWN))  dir |= 1;  // bit0 = 下
if (IsKeyDown(VK_A) || IsKeyDown(VK_LEFT))  dir |= 4;  // bit2 = 左
if (IsKeyDown(VK_D) || IsKeyDown(VK_RIGHT)) dir |= 8;  // bit3 = 右
```

要点：
- **方向被压成 4 个 bit**，多键同时按下会得到组合方向（如 `W+A` → 左上），节省带宽；
- **按住 = 持续移动，松开 = 立即停止**——与 Unity 的 `Input.GetKey` 手感一致；
- 为什么不用 `Console.ReadKey`？它依赖控制台输入缓冲区，与画面刷新（`Console.Clear/SetCursorPosition`）冲突，后台线程的 ReadKey 会丢事件 → 按键"吃不到"；
- `GetAsyncKeyState` 直接读物理按键状态，不经过控制台缓冲区，天然支持"按住"。

**(2) 主循环把输入喂进 Runner（环节 ② 的入口）**

主线程以 ~60Hz 跑渲染循环，每帧先用 `GetAsyncKeyState` 读取物理按键状态，再驱动 Runner：

```csharp
_runner.CurrentMoveDir = dir;       // 喂入本帧方向（按住=非零，松开=0）
_runner.CurrentFire    = fire;
_runner.Update();                    // 内部按 TickRate(20Hz) 决定是否真正 tick
```

注意 `Update()` 内部用 `TickTimer` 把"渲染帧(60Hz)"和"逻辑帧(20Hz)"解耦——**渲染可以比逻辑快，但逻辑推进严格 50ms 一次**，这是确定性的前提。

**(3) Runner 内部：构造输入 + 上报（环节 ②③）**

`LockstepRunner.ClientTick()` 第 2 步：

```csharp
var myInput = new PlayerInput(MyPlayerId, targetTick, CurrentMoveDir, CurrentFire);
_myInputHistory[targetTick] = myInput;   // ★ 记进历史，回滚时重放要用
OnSendInput?.Invoke(myInput);            // ★ 通过钩子真正发包
```

`OnSendInput` 是一个**注入式钩子**，由 `Program.cs` 在启动时接上：

```csharp
_runner.OnSendInput = input =>
{
    _net.Send(new { Type = MessageType.Input,
                    Tick = input.Tick, MoveDir = (int)input.MoveDir, Fire = input.Fire });
};
```

> ⚠️ **新手最常踩的"静默失效"坑就在这里**：如果忘了写 `OnSendInput = ...`，你会看到自己的 PRED 世界能动，但其他玩家眼里你纹丝不动——因为你的输入只在本地预测里跑，根本没出网。
> 验证办法：看服务端 tick 日志里你的坐标有没有随按键变化。

同时把 `myInput` 存进 `_myInputHistory`——这是**回滚时重放的素材**，下文 4.4 会用到。

---

### 4.3 服务端：收输入、产帧、广播（环节 ④⑤⑥）

**(1) 收输入并登记（环节 ④）**

每个连接有一个 `ClientSession`，后台线程 `RecvLoop()` 持续读 socket、解包、回调 `Room.HandleMessage`：

```csharp
case MessageType.Input:
    var input = new PlayerInput(session.PlayerId, /*tick*/..., /*movedir*/..., /*fire*/...);
    lock (_inputLock) { _pendingInputs[input.PlayerId] = input; }  // 覆盖式登记
```

要点：
- `_pendingInputs` 是 `Dictionary<PlayerId, PlayerInput>`。同一玩家在一个 tick 内上报多次，**只保留最后一次**（简化策略）；
- 用 `_inputLock` 保护并发——`RecvLoop` 线程写，`ProduceOneFrame` 线程读。

**(2) 产帧（环节 ⑤）—— 帧同步服务端的核心**

服务端后台线程以 50ms 节拍调用 `Room.ProduceOneFrame()`：

```csharp
_currentTick++;
var frame = new FrameData(_currentTick);
lock (_inputLock)
{
    foreach (var kv in _sessions)               // 遍历房间内每个玩家
    {
        int pid = kv.Value.PlayerId;
        if (_pendingInputs.TryGetValue(pid, out var input))
        {   input.Tick = _currentTick;          // 以服务端 tick 为准（防客户端填错）
            frame.Inputs.Add(input); }
        else
            frame.Inputs.Add(PlayerInput.Empty(pid, _currentTick));  // ★缺席补空
    }
    _pendingInputs.Clear();                     // 本 tick 处理完，清空等下一帧
}
_sim.Step(frame);                               // 服务端自己也跑一次（保留权威世界）
Broadcast(new { Type = MessageType.Frame, Frame = frame });
```

三个**新手最容易忽视**的设计点：

1. **缺席补空输入**：如果某玩家没上报（卡顿/断线），用 `PlayerInput.Empty` 兜底。**删掉这行，一个挂机玩家会把整局卡死**——这就是坑 2。
2. **以服务端 tick 为准**：客户端填的 `Tick` 可能因网络延迟而不准，统一改写成服务端的 `_currentTick`，保证帧归属正确。
3. **服务端自己跑一次 `_sim.Step(frame)`**：保留一份权威世界，用于①节流日志里打印 `hash` 做健康检查，②日后给晚加入玩家发快照。

**(3) 广播（环节 ⑥）**

`Broadcast()` 把同一份 `FrameData` 序列化后发给所有活跃会话。发送走 `BlockingCollection<byte[]>` 队列串行化，避免多线程同时写 socket。

---

### 4.4 客户端：收权威帧 + 预测 + 回滚（环节 ⑦ 的核心）

收到 `Frame` 消息后，**不立即应用**，而是入队：

```csharp
_runner.OnReceiveServerFrame(frame);   // => _pendingServerFrames.Enqueue(frame)
```

之所以入队而非立即 step：网络包可能乱序、可能跳号。必须由 `ClientTick()` **按 tick 顺序严格消费**，否则会推坏世界。

`ClientTick()` 分 4 步，这是整个示例的技术精华：

**第 1 步：消费权威帧（按序推进权威世界）**

```csharp
while (_pendingServerFrames.Count > 0)
{
    var next = _pendingServerFrames.Peek();
    if (next.Tick == _authoritativeSim.World.Tick + 1) {       // 正好接续
        _authoritativeSim.Step(next);
        LatestServerTick = next.Tick;
        _pendingServerFrames.Dequeue(); }
    else if (next.Tick <= _authoritativeSim.World.Tick) {       // 过期帧：丢
        _pendingServerFrames.Dequeue(); }
    else break;                                                  // 跳号：等后续帧补
}
```

**第 2 步：上报本帧输入**（见 4.2 第 3 点）

**第 3 步：预测推进 —— 回滚对账的精髓**

```csharp
RebuildPredictedFromAuthoritative();   // i. 把预测世界重置成权威世界快照
while (_predictedSim.World.Tick < targetTick)
{
    int pt = _predictedSim.World.Tick + 1;
    var predictedFrame = new FrameData(pt);
    foreach (var kv in _authoritativeSim.World.Players)
    {
        if (kv.Key == MyPlayerId)
            predictedFrame.Inputs.Add(_myInputHistory[pt]);     // ii. 用我的历史输入
        else
            predictedFrame.Inputs.Add(PlayerInput.Empty(...));  // iii. 别人暂时按"没动"
    }
    _predictedSim.Step(predictedFrame);                          // iv. 重放一帧
}
```

解读这段"回滚"算法：
- **i. 重置成权威快照**：`_predictedSim.SetWorld(_authoritativeSim.World.CloneSnapshot())`。这是"回滚"的字面含义——丢弃之前飘了的预测，从权威事实重新出发。**注释掉这一行，你会看到预测坐标飘了再不回来（坑 1 的变种）。**
- **ii. 用历史输入重放**：因为 4.2 第 3 步把每次本地输入都存进了 `_myInputHistory`，这里就能取出"权威 tick 之后、我自己按过的每一帧输入"，逐帧重放。
- **iii. 别人按"没动"兜底**：在对应的服务端帧到达前，我们不知道别的玩家在动什么。最朴素的预测是"假设他们静止"。更高级的实现会沿用对方上一帧输入做"惯性预测"，本示例刻意保持简单。
- **iv. 重放**：每重放一帧，`_predictedSim` 就向 `targetTick` 前进一步。最终 `_predictedSim.World.Tick == targetTick`，预测完成。

**第 4 步：老化清理**——`_myInputHistory` 只保留最近 200 帧，避免内存无限增长。

**为什么这套机制能既"低延迟"又"最终一致"？**
- **低延迟**：本地一按键，第 2 步立刻上报、第 3 步立刻预测推进，画面**当帧就响应**，不等 RTT；
- **最终一致**：服务端权威帧一到，第 1 步推进权威世界、第 3 步以权威世界为基准重放——任何预测误差都会在下一次 `ClientTick` 被"重置 + 重放"自动纠正。

---

### 4.5 渲染：用预测世界画画面（环节 ⑧）

`Program.DrawScreen()` 每当 AUTH/PRED tick 变化就刷新一次：

```
AUTH:                          PRED:
  *P1  x=1.200  y=0.400  ...     *P1  x=1.220  y=0.405  ...   ← PRED 比 AUTH 领先(手感)
    P2  x=0.000  y=0.000  ...       P2  x=0.000  y=0.000  ...
```

- **PRED 段是玩家实际看到的画面**（手感来源）；
- **AUTH 段只用于"对账"**（判断有没有飘）；
- 有按键时 PRED 领先 AUTH 1–3 帧（=预测窗口，约等于半个 RTT）；无按键时两者逐帧对齐（=回滚对账生效）。

---

### 4.6 网络协议与收发（贯穿所有环节）

所有环节的网络收发都走 [NetProtocol.cs](src/Lockstep.Shared/NetProtocol.cs) 的统一格式：

```
[4 字节大端序长度][N 字节 UTF8 JSON]
```

- `WriteMessage(obj)`：序列化 → 加长度前缀；
- `TryReadMessage()` / 各端的 `TryConsumeOneMessage()`：**循环解包**处理 TCP 粘包，长度越界会抛异常防御。

消息类型常量集中在 `MessageType`：`Hello` / `Welcome` / `Input` / `Frame` / `Goodbye` / `PlayerJoin`，用 `Type` 字段做路由分发。

> 选 JSON 是为新手友好（包内容肉眼可读）；生产项目可换 Protobuf/MessagePack 降带宽，见练习 4。

---

### 4.7 Unity 接入：把控制台渲染换成 GameObject

[Unity/LockstepUnityBridge.cs](src/Lockstep.Client/Unity/LockstepUnityBridge.cs) 是"挂到 GameObject 上"的薄桥接，**只需做三件事**，帧同步内核零改动：

1. **采集键盘**：用 `Input.GetKey(KeyCode.W)`（真正的"按住"语义，比控制台 ReadKey 自然）写入 `_runner.CurrentMoveDir`；
2. **驱动逻辑**：在 `Update()` 里调 `_runner.Update()`（TickTimer 仍负责 20Hz 固定步进）；
3. **写回 transform**：`cube.transform.position = new Vector3(p.X.ToFloat()*cell, 0, p.Y.ToFloat()*cell)`——**这是唯一一处 `ToFloat()` 的逻辑出口**，渲染边界。

关键设计：Shared 和 Client 项目**刻意没有任何 `UnityEngine` 依赖**，所以帧同步的"硬骨头"（确定性、协议、预测、回滚）能原样复用。这也是本项目把服务端做成可独立部署的 .NET Core 进程的原因——它可以跑在无 GPU 的 Linux 服务器上（见 FAQ Q2）。

---

### 4.8 对应代码位置速查

| 流程环节 | 文件 | 关键方法 / 字段 |
|----------|------|----------------|
| ① 键盘采集 | [Program.cs](src/Lockstep.Client/Program.cs) | `GetAsyncKeyState` P/Invoke |
| ② 输入打包 | [PlayerInput.cs](src/Lockstep.Shared/PlayerInput.cs) | `class PlayerInput`，4 方向位 |
| ③ 上报输入 | [LockstepRunner.cs](src/Lockstep.Client/LockstepRunner.cs) | `OnSendInput` 钩子（由 Program 注入） |
| ④ 服务端收输入 | [Room.cs](src/Lockstep.Server/Room.cs) | `HandleMessage()` → `_pendingInputs` |
| ⑤ 产帧 + 推进 | [Room.cs](src/Lockstep.Server/Room.cs) | `ProduceOneFrame()` |
| ⑥ 广播 | [Room.cs](src/Lockstep.Server/Room.cs) | `Broadcast()` |
| ⑦ 预测回滚 | [LockstepRunner.cs](src/Lockstep.Client/LockstepRunner.cs) | `ClientTick()` 四步、`RebuildPredictedFromAuthoritative()` |
| ⑧ 渲染 | [Program.cs](src/Lockstep.Client/Program.cs) | `DrawScreen()` / `RenderWorld()` |
| 网络协议 | [NetProtocol.cs](src/Lockstep.Shared/NetProtocol.cs)、[NetClient.cs](src/Lockstep.Client/NetClient.cs) | `WriteMessage` / `TryConsumeOneMessage` |
| 共享模拟 | [Simulation.cs](src/Lockstep.Shared/Simulation.cs) | `Simulation.Step(FrameData)` |
| Unity 接入 | [Unity/LockstepUnityBridge.cs](src/Lockstep.Client/Unity/LockstepUnityBridge.cs) | `class LockstepUnityBridge` |

> 💡 **阅读建议**：第一次通读按 ①→⑧ 的顺序；第二次精读时重点啃 `LockstepRunner.ClientTick()` 的四步——它一句一句对应着 4.4 节的描述。把任何一步注释掉跑一遍，马上就能理解它"为什么必须存在"。

---

## 五、运行方式（5 分钟跑起来）

### 1. 前置环境

- [.NET 8 SDK](https://dotnet.microsoft.com/download) 或更高
- 终端里 `dotnet --version` 能看到 `8.0.x` / `10.0.x` 即可
- （可选）Unity 2021.2+ 用来跑 Unity 适配，但**学习阶段不用也行**

### 2. 编译

```powershell
# 在仓库根目录执行
dotnet build LockstepDemo.sln -c Debug
```

预期输出三个 dll 都成功。

### 3. 运行（开三个终端模拟"3 个玩家"）

终端 A （服务端）：
```powershell
dotnet run --project src/Lockstep.Server -- 7777
```

终端 B （客户端 1）：
```powershell
dotnet run --project src/Lockstep.Client -- 127.0.0.1 7777 P1
```

终端 C （客户端 2，与 B 同时运行）：
```powershell
dotnet run --project src/Lockstep.Client -- 127.0.0.1 7777 P2
```

### 4. 控制台客户端怎么操作

- `W / A / S / D` 或 方向键 —— 控制移动（按住持续移动，松开立即停止）
- `Space` —— 按住开火（演示输入进帧，未做命中检测）
- `ESC` —— 退出

启动后窗口里你会看到两段表：

```
============  Lockstep Demo  ============
MyPlayerId=1  LatestServerTick=142
PRED Tick=145  AUTH Tick=142  input=2 fire=False
------------------------------------------
AUTH (server truth):
  *P1  x=1.200  y=0.400  dir=Up  fire=False
    P2  x=0.000  y=0.000  dir=-   fire=False
PRED (your screen):
  *P1  x=1.220  y=0.405  dir=Up  fire=False
    P2  x=0.000  y=0.000  dir=-   fire=False
------------------------------------------
WASD/Arrows: move(hold) | Space: fire | ESC: quit
```

- `AUTH` 段：服务端权威世界（最新事实）；
- `PRED` 段：本地预测世界（实际渲染给你的画面）；
- 带 `*` 的是你自己；
- `input=` 显示当前物理按键位图（2=按住W），`dir=` 显示玩家最后移动方向（`Up/Down/Left/Right` 或 `-` 表示静止）；
- 观察：按住 W 时，PRED 比 AUTH 领先 1–3 帧（这就是"预测手感来源"）；
  松开按键时，两者每收到一次服务端帧就对齐一次（这就是"回滚对账"后的结果）。

---

## 六、按"新手问号"读代码（推荐阅读顺序）

我建议你照下面的顺序阅读，每读一节就回头对照"二、三、四"加深概念。

### 1. 先读"什么是被同步的对象"

[PlayerInput.cs](src/Lockstep.Shared/PlayerInput.cs) ~ 60 行：
- `PlayerInput` 就是"某玩家在第几 tick 按了什么键"。注意它**只**包含 `PlayerId / Tick / MoveDir / Fire`，没有位置、血量；
- `FrameData` = 一个 tick 内**全体玩家输入**的集合，是网络层真正广播的东西。

### 2. 再读"世界状态如何确定性表达"

[FixedPoint.cs](src/Lockstep.Shared/FixedPoint.cs) ~ 60 行：
- `Raw` 是 `值 × 1000`；运算符全部走整数；只有渲染时才 `ToFloat()`。
- 这是本项目最有魔力、也最容易被新手忽略的文件，请反复看。

[WorldState.cs](src/Lockstep.Shared/WorldState.cs)：
- `PlayerEntity` 全部用 `FixedPoint`；
- `ComputeHash()` 把状态拍成一个 `long`，是回滚时"判断是否和服务器一致"的标尺；
- `CloneSnapshot()` 用于回滚时"留个备份"。

### 3. 然后读"如何把一帧输入推进成一帧世界"

[Simulation.cs](src/Lockstep.Shared/Simulation.cs) ~ 90 行：
- 全文最关键的 `Step(FrameData)`：
  - 对每条玩家输入：从 `GameConfig.MoveSpeed / TickRate` 算出本帧位移；
  - 按 `Up/Down/Left/Right` 位累加 dx/dy；
  - 加到玩家坐标上，**做边界夹紧**（再次保证确定性——所有机器求出的最终位置都一致）；
  - 推进 `World.Tick`。
- 它**完全不读外部可变状态**，所以无论在服务端跑、客户端跑、Unity 里跑，结果都一样。

### 4. 读"服务端怎么产 tick"

[Room.cs](src/Lockstep.Server/Room.cs):
- `Room.ProduceOneFrame()`：每 tick 一次，
  - 从 `_pendingInputs` 收齐每个玩家的最新一份输入；
  - 没收到输入的玩家填 `PlayerInput.Empty(...)`（**关键**：避免一个挂机玩家把整局卡死）；
  - 组装 `FrameData`，让服务端自己的 Simulation 也 step 一次；
  - 广播给所有玩家。

[Program.cs](src/Lockstep.Server/Program.cs)：
- 主线程 `Accept` 阻塞接受连接；
- 一个独立后台线程 `RoomTickLoop` 调用 `Room.Tick()`，由 `TickTimer` 决定每帧具体什么时候真正产。

### 5. 读"客户端怎么预测 + 回滚"

[LockstepRunner.cs](src/Lockstep.Client/LockstepRunner.cs) —— **整个示例的技术核心**，务必精读：

- 维护两个 `Simulation`：
    - `_authoritativeSim`：只接受服务端 FrameData 推进 → 真相；
    - `_predictedSim`：本地先跑 → 玩家看到的画面；
- 用 `_myInputHistory` 保存"我每一 tick 上报的本地输入"；
- `OnSendInput` 是一个钩子，由 `Program.cs` / UnityBridge 注入：
  LockstepRunner 每 tick 产生一份"我在本帧想做的输入"就会调用它一次，
  上层拿这个钩子把消息通过 `NetClient.Send(...)` 真正发出网包。
  **这是"本地输入 → 服务端"这条链路唯一接通点**，不接通按键只在本地预测里跑、对面永远看不到。
- `OnReceiveServerFrame(frame)` 不立刻用，而是入队 `_pendingServerFrames`，由 `ClientTick()` 按 tick 顺序消费，**避免收到乱序未来帧**；
- 每本地 `ClientTick()`：
    1. 收服务端帧（如果有过期帧丢掉；如果断号则等）；
    2. 把"我本帧想做的输入"打包（含 tick）发给服务端，并记入 history；
    3. **预测**：把 `_predictedSim` 的世界**重置成权威世界的快照**（`RebuildPredictedFromAuthoritative`），然后重复 step 到目标 tick；
    4. `_myInputHistory` 老化清理：保留最近 200 帧，节省内存。

第 3 步就是"回滚 (Rollback)"的本质。如果你去掉步骤 3i 的"重置成权威世界"那一行，会看到一个 bug：**预测里漂远了的错误坐标不会被纠正回来**。这是学习时最适合主动"改一行看会发生什么"的位置。

> ⚠️ 常见静默失效坑：早期版本的 README 漏写了 `_runner.OnSendInput = ...` 这一接线。
> 你会看到"客户端控制台里自己按键 PRED 还能动"，但其他玩家发不出去，
> 服务端产帧里这个玩家永远是空输入，**别人眼里你根本没动**。
> 想确认这条链路是否接通，最快办法是看服务端控制台日志 tick 报告里你按方向时坐标是否变化。

### 6. 读"网络协议 & 收发"

[NetProtocol.cs](src/Lockstep.Shared/NetProtocol.cs)：
- 一个标准的"长度前缀 + UTF8 JSON"二进制协议；
- `TryReadMessage(buffer,...)` 处理粘包，要在循环里用。

[NetClient.cs](src/Lockstep.Client/NetClient.cs) / [Room.cs ClientSession](src/Lockstep.Server/Room.cs)：
- 后台线程阻塞读 socket，把消息扔给上层；
- 发送方向用 `BlockingCollection` 做线程安全队列，简单可靠。

### 7. 读"TickTimer 为什么不是 Thread.Sleep"

[TickTimer.cs](src/Lockstep.Shared/TickTimer.cs)：
- 大白话版"为什么不能 `Thread.Sleep(50)` 当 tick"：OS 调度抖动让 `Sleep(50)` 经常变成 53ms 或 47ms；
- `TickTimer` 用"截止时间"驱动 tick 数：把抖动累计成整数 tick 一并放出，**长跑下 tick 数 = 真实墙钟时间 / interval**，绝不会越跑越偏。

### 8. 最后读"怎么接到 Unity"

[Unity/LockstepUnityBridge.cs](src/Lockstep.Client/Unity/LockstepUnityBridge.cs):
- Unity 适配**只剩三件**事：①采集键盘喂 `_runner.CurrentMoveDir`；②调用 `_runner.Update()`；③把 `World` 写回 Unity `transform`；
- 帧同步真正"难"的部分（确定性、协议、预测、回滚）零改动地复用，这正是因为我们在 Shared/Client 里**没用任何 Unity 依赖**。

---

## 七、常见新手小坑 & 练习题

### 坑 1：忘记用定点数，直接用 float 写 Simulation

把 `Simulation.Step` 里 `FixedPoint dx, dy` 改成 `float dx, dy`，跑两台机器看坐标会不会差 1e-6。
（练习：回答"为什么",以及"在没有 FixedPoint 的项目里，要怎么补救"。）

### 坑 2：服务端"等待玩家输入到达"导致全屋卡死

这正是 `Room.ProduceOneFrame()` 里 `PlayerInput.Empty(...)` 的存在原因；删掉那行，把一个客户端 ESC 退出，你会看到剩下的人都卡住了。

### 坑 3：用 Thread.Sleep 当 tick

把 `RoomTickLoop` 的 `Thread.Sleep(1)` 改成 `Thread.Sleep(GameConfig.ServerTickMs)`，观察"墙钟节奏"和"理想逻辑帧率"是否仍然一致（用 `LatestServerTick` 长跑几分钟看是否漂移）。

### 练习

1. ~~让客户端模拟"按键松开后停止"~~：已由 `GetAsyncKeyState` 原生实现——改为：把 `GetAsyncKeyState` 换回 `Console.ReadKey`，观察"按住移动"为什么失效。
2. 加一种"碰撞"，模拟两个玩家撞一起后停下；条件必须只依赖当前 World 与输入。
3. 在 `LockstepRunner` 里写一个"走错了就立刻回滚并报警"的运行校验：当 `LatestServerTick` 上来时，把它和"同 tick 的预测世界 CloneSnapshot"做 `ComputeHash` 比对，不同就打日志。这是几乎每个上线帧同步项目都内置的"飘变检测"。
4. 把 NetProtocol 改成 `MessagePack` 或 `Protobuf` 看带宽差异（本示例为了新手友好起见用了 JSON）。

---

## 八、常见 FAQ

**Q1: 真实上线的帧同步游戏也用这套架构吗？**
基本骨架一致（共享内核 + 服务端权威 + 客户端预测回滚）。差异主要在：
- 定点数精度（本示例 1000 倍只够教学；真实多用 Q32.32 或表查法）；
- 网络协议（真实项目用更轻量二进制）；
- 输入预测算法（如 lookahead + interpolation blend 等）；
- 服务端会用"延迟缓冲 + 输入时间戳"做更复杂的时间管理。

但**主心骨的服务端包输入、客户端预测回滚、确定性共享内核**这套经典结构是不变的。**先吃透本示例，再去看商业项目代码会轻松很多。**

**Q2: 为什么不用 Unity 同时当服务端？**
为了**让服务端代码能跑在便宜的无 GPU Linux 服务器**，因此刻意避开 UnityEngine 依赖。本示例的 Server 项目可独立部署。Unity 只作客户端。

**Q3: Unity 接入要安装什么？**
1) Unity 2021.2+ 默认含 System.Text.Json；老版本可在 Unity 项目里用 NuGet for Unity 装下 `System.Text.Json`，或把本示例中的 JSON 序列化替换成 Newtonsoft.Json；
2) 把以下三个地方的 .cs 全部复制到 Unity 工程：
   - `src/Lockstep.Shared/*.cs`
   - `src/Lockstep.Client/NetClient.cs`、`LockstepRunner.cs`
   - `src/Lockstep.Client/Unity/LockstepUnityBridge.cs`
3) 给 Unity 工程 Project Settings -> Scripting Define Symbols 加入 `UNITY_ENGINE`；
4) 把 `LockstepUnityBridge` 挂到任意 GameObject，填好 Inspector 上的 host/port/playerName 即可。

**Q4: 按住 W/A/S/D 会持续移动吗？**
是的！控制台版本使用 Windows 原生 `GetAsyncKeyState` API 直接读取物理按键状态，**按住即持续移动，松开即停止**，与 Unity 的 `Input.GetKey(KeyCode.W)` 手感一致。早期版本使用 `Console.ReadKey` 只能"按一次移动一次"，已弃用。

---

## 九、文件清单速查

| 库 | 文件 | 一句话作用 |
|----|------|----------|
| Shared | [FixedPoint.cs](src/Lockstep.Shared/FixedPoint.cs) | 定点数（确定性基石） |
| Shared | [PlayerInput.cs](src/Lockstep.Shared/PlayerInput.cs) | 玩家单帧输入 + 帧数据容器 |
| Shared | [WorldState.cs](src/Lockstep.Shared/WorldState.cs) | 世界状态 / 快照哈希 |
| Shared | [Simulation.cs](src/Lockstep.Shared/Simulation.cs) | 确定性模拟（推进一帧） |
| Shared | [TickTimer.cs](src/Lockstep.Shared/TickTimer.cs) | 稳定节拍器 |
| Shared | [NetProtocol.cs](src/Lockstep.Shared/NetProtocol.cs) | 网络二进制协议（长度前缀+JSON） |
| Shared | [GameConfig.cs](src/Lockstep.Shared/GameConfig.cs) | 全局常量 |
| Server | [Program.cs](src/Lockstep.Server/Program.cs) | 启动 + Tick 循环 |
| Server | [Room.cs](src/Lockstep.Server/Room.cs) | 房间 / 会话 / 帧调度 / 广播 |
| Client | [Program.cs](src/Lockstep.Client/Program.cs) | 控制台入口 + 键盘 + 渲染 |
| Client | [NetClient.cs](src/Lockstep.Client/NetClient.cs) | TCP 收发线程封装 |
| Client | [LockstepRunner.cs](src/Lockstep.Client/LockstepRunner.cs) | 客户端业务核心（预测回滚） |
| Client | [Unity/LockstepUnityBridge.cs](src/Lockstep.Client/Unity/LockstepUnityBridge.cs) | Unity 适配 MonoBehaviour |

祝学习顺利！如果某段代码"看不出为什么必须存在"，把它注释掉再跑一遍马上就能理解。
