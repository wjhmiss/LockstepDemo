# ET10 框架全方位新手学习教程

> 基于 `d:\Unity\LockstepDemo\ET` 项目完整代码库深入分析编写
> 适用版本：ET10（代号"昭君"）
> 本教程从基础到进阶，系统性地介绍ET框架的各项核心功能及其在新Unity项目中的实际应用方法

---

## 目录

- [第一章 环境搭建与项目运行](#第一章-环境搭建与项目运行)
- [第二章 ET架构总览](#第二章-et架构总览)
- [第三章 Entity系统 - 一切皆实体](#第三章-entity系统---一切皆实体)
- [第四章 Component系统 - 组件式设计](#第四章-component系统---组件式设计)
- [第五章 System系统 - 数据与逻辑分离](#第五章-system系统---数据与逻辑分离)
- [第六章 EventSystem事件系统](#第六章-eventsystem事件系统)
- [第七章 ETTask异步系统](#第七章-ettask异步系统)
- [第八章 Timer定时器系统](#第八章-timer定时器系统)
- [第九章 Fiber纤程系统](#第九章-fiber纤程系统)
- [第十章 网络系统](#第十章-网络系统)
- [第十一章 Actor模型](#第十一章-actor模型)
- [第十二章 配置系统](#第十二章-配置系统)
- [第十三章 帧同步实战](#第十三章-帧同步实战)
- [第十四章 常见问题与最佳实践](#第十四章-常见问题与最佳实践)

---

## 第一章 环境搭建与项目运行

### 1.1 环境要求

| 工具 | 版本要求 | 说明 |
|------|---------|------|
| Unity | 2022.3.62 | 初学者请严格使用此版本 |
| .NET | 8.0 | 通过Visual Studio Installer安装 |
| IDE | Rider 2024.3+ | 不支持VS，新人容易出问题 |
| PowerShell | 7+ | 必须使用pwsh，非Windows自带powershell |

> **重要提示**：整个过程请开启全局翻墙，否则各种unity包、nuget包下载不下来。

### 1.2 初始化步骤

1. **克隆项目**：一定要clone一个新的工程
2. **执行初始化脚本**：在项目根目录执行

```powershell
pwsh ./Scripts/Initialize-Project.ps1
```

3. **打开Unity工程**：用UnityHub打开`ET`文件夹所在目录
4. **配置外部工具**：Unity菜单 -> Edit -> Preferences -> External Tools，选择Rider，勾选前两个Generate .csproj files
5. **打开C#项目**：Unity菜单 Assets -> Open C# Project，会自动打开ET.sln
6. **编译ET.sln**：注意要翻墙，否则nuget包下载不下来

### 1.3 运行项目

1. 双击 `Packages/cn.etetet.statesync/Scenes/Init` 场景（或lockstep场景）
2. 点击Play(▶)即可运行

### 1.4 独立启动服务器

```powershell
# 以管理员身份运行UnityHub启动Unity
# Unity菜单 -> ET -> Loader -> Server Tools -> Start Server(Single Process)
# 然后把GlobalConfig中CodeMode改成Client，点击Unity Play登录
```

独立运行服务器的命令行方式：

```powershell
dotnet.exe Bin/ET.App.dll --Console=1
```

> 注意：运行目录是Bin的上一层目录（Unity目录），不是Bin目录本身。

### 1.5 热重载

1. Unity菜单 -> Edit -> Preferences -> General -> ScriptChangesWhilePlaying -> 选择 `RecompileAfterFinishedPlaying`
2. 运行后修改并编译代码，按 **F7** 或 Unity菜单 -> ET -> Reload 即可热重载

---

## 第二章 ET架构总览

### 2.1 ET10核心理念

ET10是面向AI编码时代的下一代游戏开发框架，核心理念：

- **ECS架构**：Entity（数据）- Component（数据）- System（逻辑）完全分离
- **双端C#开发**：客户端服务端共享代码
- **模块化Package**：功能按包组织，松耦合设计
- **分布式支持**：多进程、多服务器、纤程（Fiber）架构
- **热更新**：代码、资源、配置三位一体热更新

### 2.2 包层级架构

ET采用分层包依赖设计，参考 [cn.etetet.harness/AGENTS.md](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.harness/AGENTS.md)：

```
第1层（基础层）
├── cn.etetet.core          核心框架（Entity/Component/EventSystem等）
├── cn.etetet.proto          协议定义
├── cn.etetet.loader         加载器

第2层（功能基础）
├── cn.etetet.unit           单位系统
├── cn.etetet.startconfig    服务器配置
├── cn.etetet.yooassets      资源加载
├── cn.etetet.yiui           UI框架
├── cn.etetet.behaviortree   行为树

第3层（业务基础）
├── cn.etetet.numeric        数值系统（依赖unit）
├── cn.etetet.move           移动系统（依赖unit）
├── cn.etetet.netinner       内网消息（依赖startconfig）

第4层（高级功能）
├── cn.etetet.actorlocation  Location消息系统
├── cn.etetet.map            地图系统
├── cn.etetet.aoi            九宫格AOI

第5层（游戏入口）
├── cn.etetet.statesync      状态同步
├── cn.etetet.lockstep       帧同步
├── cn.etetet.test           测试系统
```

### 2.3 依赖原则

1. 包之间只能单向依赖，不能相互依赖
2. 高层包可以依赖低层包，反之不行
3. 跨包访问必须显式声明依赖
4. 修改依赖时要递归加上依赖的依赖

### 2.4 代码分层

每个Package内部代码分为：

| 层级 | 说明 | 示例 |
|------|------|------|
| `Scripts/Model/Share` | 共享数据模型（Entity定义） | `Room.cs` |
| `Scripts/Model/Client` | 客户端独有数据模型 | `LSClientUpdater.cs` |
| `Scripts/Model/Server` | 服务端独有数据模型 | `MatchComponent.cs` |
| `Scripts/Hotfix/Share` | 共享热更逻辑（System） | `RoomSystem.cs` |
| `Scripts/Hotfix/Client` | 客户端独有热更逻辑 | `LSClientUpdaterSystem.cs` |
| `Scripts/Hotfix/Server` | 服务端独有热更逻辑 | `MatchComponentSystem.cs` |

---

## 第三章 Entity系统 - 一切皆实体

### 3.1 Entity设计理念

ET框架的核心设计是"一切皆实体"。Entity是所有数据的基类，既可以作为容器挂载Component，也可以作为Child挂在其他Entity下面。

参考源码：[Entity.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Entity/Entity.cs)

### 3.2 Entity核心字段

```csharp
[MemoryPackable(GenerateType.NoGenerate)]
public abstract partial class Entity: DisposeObject
{
    // 实例ID，全局唯一，用于标识对象身份（对象池回收后会变化）
    [MemoryPackIgnore]
    [BsonIgnore]
    public long InstanceId { get; protected set; }

    // 唯一ID，持久化使用，对象池回收后不变
    [BsonId]
    public long Id { get; protected set; }

    // 父实体
    private Entity parent;
    public Entity Parent { get; protected set; }

    // 所属Scene（域）
    protected IScene iScene;
    public IScene IScene { get; protected set; }

    // 子实体集合
    protected ChildrenCollection children;
    public ChildrenCollection Children { get; }

    // 组件集合
    protected ComponentsCollection components;
    public ComponentsCollection Components { get; }

    // 是否已释放
    public bool IsDisposed => this.InstanceId == 0;
}
```

### 3.3 Entity状态管理

Entity使用位标志管理状态：

```csharp
[Flags]
public enum EntityStatus: byte
{
    None = 0,
    IsFromPool = 1,                    // 是否来自对象池
    IsRegister = 1 << 1,               // 是否已注册到System
    IsComponent = 1 << 2,              // 是否作为Component挂载
    NoDeserializeSystem = 1 << 3,      // 不需要执行反序列化System
    IsSerializeWithParent = 1 << 4,    // 是否跟随Parent序列化
}
```

### 3.4 创建自定义Entity

**示例：定义一个玩家实体**

```csharp
// 文件位置: Scripts/Model/Share/Player.cs
using System;

namespace ET
{
    [ComponentOf(typeof(Scene))]
    public class Player : Entity, IAwake<string>, IDestroy
    {
        public string Account { get; set; }
        public long UnitId { get; set; }
        public int Level { get; set; }
    }
}
```

**关键点**：
- 继承 `Entity` 类
- 实现 `IAwake` 接口表示需要Awake生命周期
- 使用 `[ComponentOf]` 特性标注可以挂载到哪种Entity上
- Entity只包含数据，不包含方法

### 3.5 添加和移除Child

```csharp
// 添加Child（自动生成Id）
Player player = scene.AddChild<Player>();

// 带参数添加Child
Player player = scene.AddChild<Player, string>("account123");

// 指定Id添加Child
Player player = scene.AddChildWithId<Player>(1001);

// 获取Child
Player player = scene.GetChild<Player>(1001);

// 移除Child
scene.RemoveChild(1001);
```

源码参考：[Entity.cs AddChild方法](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Entity/Entity.cs#L791-L821)

### 3.6 序列化机制

Entity支持两种序列化：
- **MemoryPack**：用于网络传输，高性能0GC
- **MongoBson**：用于数据库存储

```csharp
// 标记需要序列化的字段
[MemoryPackable(GenerateType.NoGenerate)]
public partial class Player : Entity
{
    // MemoryPackInclude标记的字段会被序列化
    [MemoryPackInclude]
    public string Account { get; set; }
}

// ISerializeToEntity接口表示该Entity会跟随父Entity序列化到数据库
public class Player : Entity, ISerializeToEntity
{
}
```

### 3.7 实战示例：Room实体

参考 [Room.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.lockstep/Scripts/Model/Share/Room.cs)：

```csharp
[ComponentOf]
public class Room: Entity, IScene, IAwake, IUpdate
{
    public Fiber Fiber { get; set; }      // 房间所属纤程
    public int SceneType { get; set; }     // 场景类型
    public string Name { get; set; }       // 房间名

    public long StartTime { get; set; }    // 开始时间
    public FrameBuffer FrameBuffer { get; set; }  // 帧缓存
    public FixedTimeCounter FixedTimeCounter { get; set; }  // 固定时间计数器

    public List<long> PlayerIds { get; } = new(LSConstValue.MatchCount);  // 玩家列表

    public int PredictionFrame { get; set; } = -1;  // 预测帧
    public int AuthorityFrame { get; set; } = -1;   // 权威帧

    public Replay Replay { get; set; } = new();      // 录像存档

    private EntityRef<LSWorld> lsWorld;
    public LSWorld LSWorld
    {
        get => this.lsWorld;
        set
        {
            this.AddChild(value);  // LSWorld作为Child挂载
            this.lsWorld = value;
        }
    }
}
```

---

## 第四章 Component系统 - 组件式设计

### 4.1 Component的本质

在ET中，**Component本质上也是Entity**。Component和Entity是同一个类，只是挂载方式不同：
- 作为Child挂载：`AddChild<T>()`
- 作为Component挂载：`AddComponent<T>()`

这种设计使得Component也可以挂载自己的Component和Child，形成树状结构。

### 4.2 添加Component

```csharp
// 添加无参数Component
MoveComponent moveComponent = player.AddComponent<MoveComponent>();

// 添加带参数Component（需要实现IAwake<P1>）
player.AddComponent<ItemsComponent, int>(100);  // 初始容量100

// 添加带多个参数Component
player.AddComponent<BuffComponent, int, float>(10, 1.5f);
```

源码参考：[Entity.cs AddComponent方法](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Entity/Entity.cs#L757-L783)

### 4.3 获取Component

```csharp
// 获取Component
MoveComponent moveComponent = player.GetComponent<MoveComponent>();

// 通过Type获取
Entity comp = player.GetComponent(typeof(MoveComponent));

// 移除Component
player.RemoveComponent<MoveComponent>();
```

### 4.4 Component的特性标注

```csharp
// 标注这个Component可以挂载到哪种Entity上
[ComponentOf(typeof(Player))]
public class SpellComponent : Entity, IAwake, IUpdate
{
    public List<Spell> Spells { get; set; }
}

// 标注这个Component可以挂载到任何Entity上
[ComponentOf]
public class MoveComponent : Entity, IAwake, IUpdate
{
}

// 标注这个Component作为Child挂载（不是Component）
[ChildOf(typeof(ItemsComponent))]
public class Item : Entity, IAwake<int>
{
    public int ConfigId { get; set; }
    public int Count { get; set; }
}
```

### 4.5 对象池机制

ET的Entity支持对象池，减少GC：

```csharp
// 从对象池创建（isFromPool = true）
Player player = scene.AddChild<Player>(true);

// 使用IPool接口标记支持对象池
public class Player : Entity, IAwake, IPool
{
}

// Entity回收后会回到对象池
player.Dispose();
```

### 4.6 实战示例：给Player挂载组件

```csharp
// 创建玩家
Player player = scene.AddChild<Player, string>("account001");

// 给玩家挂载各种功能组件
player.AddComponent<MoveComponent>();        // 移动功能
player.AddComponent<ItemsComponent>();       // 背包功能
player.AddComponent<SpellComponent>();       // 技能功能
player.AddComponent<BuffComponent>();        // Buff功能
player.AddComponent<NumericComponent>();     // 数值属性

// NPC只需要部分组件
NPC npc = scene.AddChild<NPC>();
npc.AddComponent<MoveComponent>();           // NPC也能移动
npc.AddComponent<SpellComponent>();         // NPC也能施法
// NPC不需要背包，不挂ItemsComponent
```

---

## 第五章 System系统 - 数据与逻辑分离

### 5.1 System设计理念

ET的核心创新是**数据与逻辑完全分离**：
- **Entity/Component**：只包含数据，不包含方法
- **System**：包含逻辑，以扩展方法形式存在，可放到热更dll中

### 5.2 System的生命周期接口

参考 [IAwakeSystem.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Entity/IAwakeSystem.cs)：

| 接口 | 触发时机 | 说明 |
|------|---------|------|
| `IAwake` | AddComponent/AddChild时 | 只触发一次，可带参数 |
| `IUpdate` | 每帧 | 帧更新 |
| `ILateUpdate` | 每帧LateUpdate | 晚于Update |
| `IDestroy` | Dispose时 | 销毁时触发 |
| `ISerialize` | 序列化前 | 序列化前触发 |
| `IDeserialize` | 反序列化后 | 反序列化后触发 |
| `IGetComponentSys` | GetComponent时 | 获取组件时触发 |

### 5.3 编写System类

**示例：Player的System**

```csharp
// 文件位置: Scripts/Hotfix/Share/PlayerSystem.cs
using System;

namespace ET
{
    // 使用EntitySystemOf标注这是哪个Entity的System
    [EntitySystemOf(typeof(Player))]
    public static partial class PlayerSystem
    {
        // Awake系统，对应IAwake<string>接口
        [EntitySystem]
        private static void Awake(this Player self, string account)
        {
            self.Account = account;
            self.Level = 1;
            Log.Info($"玩家创建: {account}");
        }

        // Destroy系统，对应IDestroy接口
        [EntitySystem]
        private static void Destroy(this Player self)
        {
            Log.Info($"玩家销毁: {self.Account}");
            self.Account = null;
            self.Level = 0;
        }

        // Update系统，对应IUpdate接口
        [EntitySystem]
        private static void Update(this Player self)
        {
            // 每帧执行的逻辑
        }

        // 自定义扩展方法
        public static void LevelUp(this Player self)
        {
            self.Level++;
            // 发布升级事件
            EventSystem.Instance.Publish(self.IScene, new PlayerLevelUpEvent
            {
                PlayerId = self.Id,
                NewLevel = self.Level
            });
        }
    }
}
```

### 5.4 System的关键特性

```csharp
// EntitySystemOf: 标注这个静态类是哪个Entity的System
[EntitySystemOf(typeof(Player))]
public static partial class PlayerSystem
{
}

// EntitySystem: 标注这个方法是生命周期方法
[EntitySystem]
private static void Awake(this Player self) { }

// FriendOf: 允许访问Entity的私有字段（不推荐，应该用属性）
[FriendOf(typeof(Player))]
public static class PlayerHelper
{
}
```

### 5.5 实战示例：TimerComponentSystem

参考 [TimerComponentSystem.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Timer/TimerComponentSystem.cs)：

```csharp
[EntitySystemOf(typeof(TimerComponent))]
public static partial class TimerComponentSystem
{
    [EntitySystem]
    private static void Awake(this TimerComponent self)
    {
        // Awake时把TimerComponent设置到Scene上
        self.GetParent<Scene>().TimerComponent = self;
    }

    [EntitySystem]
    private static void Update(this TimerComponent self)
    {
        if (self.timeId.Count == 0)
        {
            return;
        }

        long timeNow = self.GetNow();

        // 检查是否有定时器到期
        if (timeNow < self.minTime)
        {
            return;
        }

        // 处理到期的定时器...
    }
}
```

### 5.6 Scene的定义

Scene是特殊的Entity，实现了IScene接口，作为Entity树的根节点：

参考 [Scene.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Entity/Scene.cs)：

```csharp
[EnableMethod]
[ChildOf]
public partial class Scene: Entity, IScene
{
    public Fiber Fiber { get; set; }     // 所属纤程
    public string Name { get; set; }      // 场景名
    public int SceneType { get; set; }    // 场景类型

    public Scene(Fiber fiber, long id, int sceneType, string name)
    {
        this.Id = id;
        this.Name = name;
        this.InstanceId = fiber.NewInstanceId();
        this.SceneType = sceneType;
        this.Fiber = fiber;
        this.IScene = this;  // Scene的IScene指向自己
        this.IsRegister = true;
    }
}
```

---

## 第六章 EventSystem事件系统

### 6.1 事件系统概述

EventSystem是ET实现"数据驱动逻辑"的核心机制。参考源码：[EventSystem.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/World/EventSystem/EventSystem.cs)

### 6.2 Publish/Subscribe事件

**定义事件结构体**：

```csharp
// 事件必须是struct（值类型），减少GC
public struct PlayerLevelUpEvent
{
    public long PlayerId;
    public int NewLevel;
}
```

**订阅事件**：

```csharp
// 使用EventAttribute标注事件处理器
[Event]
public class PlayerLevelUpEvent_ShowUI : AEvent<Scene, PlayerLevelUpEvent>
{
    public override async ETTask Handle(Scene scene, PlayerLevelUpEvent args)
    {
        // UI模块订阅升级事件，刷新UI
        Log.Info($"UI显示: 玩家{args.PlayerId}升到{args.NewLevel}级");
        await ETTask.CompletedTask;
    }
}

[Event]
public class PlayerLevelUpEvent_ShowEffect : AEvent<Scene, PlayerLevelUpEvent>
{
    public override async ETTask Handle(Scene scene, PlayerLevelUpEvent args)
    {
        // 特效模块订阅升级事件，播放特效
        Log.Info($"播放升级特效");
        await ETTask.CompletedTask;
    }
}
```

**发布事件**：

```csharp
// 同步发布（不等所有订阅者处理完）
EventSystem.Instance.Publish(scene, new PlayerLevelUpEvent
{
    PlayerId = player.Id,
    NewLevel = player.Level
});

// 异步发布（等待所有订阅者处理完）
await EventSystem.Instance.PublishAsync(scene, new PlayerLevelUpEvent
{
    PlayerId = player.Id,
    NewLevel = player.Level
});
```

### 6.3 Invoke调用

Invoke类似函数调用，必须有被调用方，否则异常。与Publish的区别：

```csharp
// Publish：事件，抛出去可以没人订阅，调用者跟被调用者属于两个模块
// Invoke：类似函数，必须有被调用方，调用者跟被调用者属于同一模块

// 定义Invoke参数
public struct TimerCallback
{
    public EntityRef<Entity> Args;
}

// 定义Invoke处理器
[Invoke]
public class TimerCallbackHandler : AInvokeHandler<TimerCallback>
{
    public override void Handle(TimerCallback args)
    {
        // 处理定时器回调
    }
}

// 调用Invoke
EventSystem.Instance.Invoke(timerType, new TimerCallback { Args = entity });

// TryInvoke：如果没注册处理器不会异常
EventSystem.Instance.TryInvoke(timerType, new TimerCallback { Args = entity });
```

### 6.4 事件系统的SceneType过滤

事件可以指定在哪种SceneType下触发：

```csharp
// 指定只在Map场景触发
[Event(SceneType.Map)]
public class PlayerDieEvent_MapHandler : AEvent<Scene, PlayerDieEvent>
{
    public override async ETTask Handle(Scene scene, PlayerDieEvent args)
    {
        await ETTask.CompletedTask;
    }
}
```

---

## 第七章 ETTask异步系统

### 7.1 ETTask概述

ETTask是ET自研的异步系统，支持async/await语法，0GC，且支持上下文传递。参考源码：[ETTask.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/ETTask/ETTask.cs)

### 7.2 ETTask基本使用

```csharp
// 创建ETTask（类似TaskCompletionSource）
ETTask tcs = ETTask.Create(true);  // true表示使用对象池

// 等待完成
await tcs;

// 设置结果
tcs.SetResult();

// 设置异常
tcs.SetException(new Exception("出错了"));
```

### 7.3 异步方法编写

```csharp
// 异步方法返回ETTask
public static async ETTask LoginAsync(this Player self)
{
    // 发送登录消息并等待响应
    R2C_Login response = await self.GetParent<Session>().Call(new C2R_Login
    {
        Account = self.Account
    }) as R2C_Login;

    // await后需要检查Entity是否已释放
    if (self.IsDisposed)
    {
        return;
    }

    Log.Info($"登录成功: {response.Token}");
}
```

### 7.4 Coroutine()与await的区别

```csharp
// Coroutine(): 不等待，类似"发射后不管"
self.LoginAsync().Coroutine();

// await: 等待异步完成
await self.LoginAsync();
```

### 7.5 上下文传递

ETTask支持传递上下文，替代CancellationToken：

```csharp
// 创建带上下文的ETTask
ETTask tcs = ETTask.Create(true);

// 设置上下文
tcs.Coroutine(cancellationToken);

// 在await时获取上下文
ETCancellationToken cancellationToken = await ETTask.GetContextAsync<ETCancellationToken>();
cancellationToken?.Add(CancelAction);

// 换新的上下文
await tcs.NewContext(newContext);
```

### 7.6 EntityRef安全

在async/await后，Entity可能已被销毁，必须使用EntityRef：

```csharp
public static async ETTask DoSomethingAsync(this Player self)
{
    // 保存引用
    EntityRef<Player> playerRef = self;

    await self.GetParent<Session>().Call(new SomeMessage());

    // await后重新获取
    Player player = playerRef.Entity;
    if (player == null)
    {
        return;  // Entity已销毁
    }

    // 安全使用
    player.Level = 10;
}
```

---

## 第八章 Timer定时器系统

### 8.1 TimerComponent概述

TimerComponent是ET的定时器系统，支持一次定时器、重复定时器、等待定时器。参考源码：[TimerComponent.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Timer/TimerComponent.cs) 和 [TimerComponentSystem.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Timer/TimerComponentSystem.cs)

### 8.2 定时器类型

```csharp
public enum TimerClass
{
    None,
    OnceTimer,       // 一次定时器（回调式）
    OnceWaitTimer,   // 一次等待定时器（await式）
    RepeatedTimer,   // 重复定时器
}
```

### 8.3 使用定时器

```csharp
// 获取TimerComponent
TimerComponent timerComponent = scene.TimerComponent;

// 1. 等待一段时间（await方式，逻辑连贯）
await timerComponent.WaitAsync(5000);  // 等待5秒
Log.Info("5秒后执行");

// 2. 等待到指定时间
await timerComponent.WaitTillAsync(TimeInfo.Instance.ServerNow() + 5000);

// 3. 等待一帧
await timerComponent.WaitFrameAsync();

// 4. 创建一次定时器（回调式，可热更）
long timerId = timerComponent.NewOnceTimer(
    TimeInfo.Instance.ServerNow() + 5000,  // 触发时间
    TimerType.PlayerRevive,                 // 定时器类型
    player                                  // 参数
);

// 5. 创建重复定时器
long repeatedTimerId = timerComponent.NewRepeatedTimer(
    1000,              // 每隔1秒
    TimerType.PlayerRecover,
    player
);

// 6. 移除定时器
timerComponent.Remove(ref timerId);
```

### 8.4 回调式定时器的处理

```csharp
// 定义定时器回调处理器
[Invoke]
public class PlayerReviveTimerHandler : AInvokeHandler<TimerCallback>
{
    public override void Handle(TimerCallback args)
    {
        Player player = args.Args.Entity as Player;
        if (player == null)
        {
            return;
        }
        player.Revive();
    }
}

// 定义定时器类型
public static class TimerType
{
    public const int PlayerRevive = 1;
    public const int PlayerRecover = 2;
}
```

### 8.5 两种定时器的选择

| 方式 | 优点 | 缺点 | 适用场景 |
|------|------|------|---------|
| `WaitAsync` | 逻辑连贯，代码清晰 | 不能热更 | 等待时间短，需要逻辑连贯 |
| `NewOnceTimer` | 可以热更 | 回调式，逻辑不连贯 | 等待时间长，需要热更 |

---

## 第九章 Fiber纤程系统

### 9.1 Fiber概述

Fiber（纤程）是ET8引入的重要概念，类似Erlang的进程。每个Fiber有自己的：
- Scene树
- EntitySystem
- Mailboxes（消息队列）
- ThreadSynchronizationContext（同步上下文）

参考源码：[Fiber.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/World/Fiber/Fiber.cs)

### 9.2 Fiber的调度方式

```csharp
public enum SchedulerType
{
    MainThread,      // 主线程调度
    ThreadPool,      // 线程池调度
    Parent,          // 父Fiber调度（跟父Fiber同线程）
}
```

### 9.3 创建Fiber

```csharp
// 在当前Fiber中创建子Fiber（跟当前Fiber同线程）
Fiber childFiber = await fiber.CreateFiber(rootId, SceneType.Map, "MapFiber");

// 创建独立线程的Fiber
long fiberId = await fiber.CreateFiber(SchedulerType.ThreadPool, rootId, SceneType.Battle, "BattleFiber");

// 移除Fiber
await fiber.RemoveFiber(fiberId);
```

### 9.4 Fiber的应用场景

```
主Fiber（主线程）
├── 网络Fiber（独立线程）- 处理网络IO
├── 寻路Fiber（独立线程）- 处理寻路计算
├── 帧同步逻辑Fiber（独立线程）- 帧同步逻辑
└── 表现层Fiber（主线程）- 渲染表现
```

### 9.5 Fiber间通信

Fiber之间通过Actor消息通信：

```csharp
// Fiber A 发送消息给 Fiber B
// 通过ActorId定位目标Fiber
ActorId targetActorId = new ActorId(fiberId, instanceId);

// 发送Actor消息
await ActorMessageSenderComponent.Instance.Send(targetActorId, new SomeMessage());
```

### 9.6 Singleton与Fiber

```csharp
// 全局Singleton（跨Fiber共享）
World.Instance.AddSingleton<EventSystem>();

// Fiber级Singleton（每个Fiber独立）
fiber.AddSingleton<SomeFiberSingleton>();
```

参考源码：[World.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/World/World.cs) 和 [Singleton.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/World/Singleton.cs)

---

## 第十章 网络系统

### 10.1 网络架构

ET网络层支持多种协议：
- **KCP**：可靠UDP，低延迟，丢包20%不卡
- **TCP**：可靠传输
- **WebSocket**：WebGL支持

参考 [AService.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Network/AService.cs)

### 10.2 Session网络会话

Session是网络通信的基础，表示一个网络连接：

```csharp
// 创建Session
Session session = NetComponent.Instance.Create(address);

// 发送消息（不等待响应）
session.Send(new C2R_Login { Account = "test" });

// 发送RPC消息（等待响应）
R2C_Login response = (R2C_Login)await session.Call(new C2R_Login { Account = "test" });

// 销毁Session
session.Dispose();
```

### 10.3 消息处理器

**Session消息处理器**：

```csharp
// 处理客户端发送给服务端的消息
[MessageSessionHandler]
public class C2G_EnterMapHandler : MessageSessionHandler<C2G_EnterMap, G2C_EnterMap>
{
    protected override async ETTask Run(Session session, C2G_EnterMap message, G2C_EnterMap response)
    {
        // 处理进入地图逻辑
        Player player = session.GetComponent<Player>();
        await MapHelper.EnterMap(player);

        response.PlayerId = player.Id;
    }
}
```

**普通消息处理器**：

```csharp
// 处理服务端内部消息
[MessageHandler]
public class C2Room_CheckHashHandler : MessageHandler<C2Room_CheckHash>
{
    protected override async ETTask Run(Scene scene, C2Room_CheckHash message)
    {
        // 处理Hash校验
    }
}
```

### 10.4 消息定义

消息使用Proto定义，自动生成C#代码：

```csharp
// 消息定义后自动生成
[ResponseType(nameof(G2C_Login))]
public partial class C2G_Login : MessageObject, IRequest
{
    public string Account { get; set; }
}

public partial class G2C_Login : MessageObject, IResponse
{
    public int Error { get; set; }
    public string Token { get; set; }
}
```

---

## 第十一章 Actor模型

### 11.1 Actor概述

ET提供了Entity级别的Actor模型。Entity挂上MailBoxComponent组件就成为Actor，只需要知道Entity的InstanceId就能发消息。

参考 [Book/5.4Actor模型.md](file:///d:/Unity/LockstepDemo/ET/Book/5.4Actor模型.md)

### 11.2 创建Actor

```csharp
// 给Entity挂载MailBoxComponent，成为Actor
session.AddComponent<MailBoxComponent, string>(MailboxType.MessageDispatcher);
```

### 11.3 发送Actor消息

```csharp
// 通过ActorId发送消息
ActorId targetActorId = new ActorId(fiberId, instanceId);

// Send（不等待响应）
ActorMessageSenderComponent.Instance.Send(targetActorId, new Actor_Test { Info = "hello" });

// Call（RPC，等待响应）
var response = await ActorMessageSenderComponent.Instance.Call(targetActorId, new Actor_TransferRequest());
```

### 11.4 处理Actor消息

```csharp
// 处理Send的消息
[ActorMessageHandler]
public class Actor_TestHandler : MessageHandler<Unit, Actor_Test>
{
    protected override async ETTask Run(Unit unit, Actor_Test message)
    {
        Log.Debug(message.Info);
        await ETTask.CompletedTask;
    }
}

// 处理Rpc消息
[ActorMessageHandler]
public class Actor_TransferHandler : MessageRpcHandler<Unit, Actor_TransferRequest, Actor_TransferResponse>
{
    protected override async ETTask Run(Unit unit, Actor_TransferRequest message, Actor_TransferResponse response)
    {
        // 处理转移逻辑
    }
}
```

### 11.5 Actor Location

Actor Location用于跨进程Actor通信，通过Location服务定位Actor位置：

```csharp
// 发送消息到指定EntityId（自动通过Location服务查找位置）
await ActorLocationSenderComponent.Instance.Send(entityId, new SomeMessage());

// RPC调用
var response = await ActorLocationSenderComponent.Instance.Call(entityId, new SomeRequest());
```

### 11.6 邮箱类型

```csharp
// GateSession邮箱：收到消息立即转发给客户端
session.AddComponent<MailBoxComponent, string>(MailboxType.GateSession);

// MessageDispatcher邮箱：收到消息分发到具体Handler（默认）
session.AddComponent<MailBoxComponent, string>(MailboxType.MessageDispatcher);

// UnOrdered邮箱：无序处理
session.AddComponent<MailBoxComponent, string>(MailboxType.UnOrderedMessage);
```

---

## 第十二章 配置系统

### 12.1 Luban配置系统

ET使用Luban作为配置系统，Excel编写配置 -> Luban导出C#代码和数据。

参考 [cn.etetet.config](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.config) 包

### 12.2 配置目录结构

```
cn.etetet.config/
├── Luban/
│   └── Config/
│       ├── Base/
│       │   ├── __tables__.xlsx      # 表注册
│       │   ├── __beans__.xlsx       # Bean定义
│       │   └── __enums__.xlsx       # 枚举定义
│       ├── Datas/                    # 数据表
│       └── Defines/                  # 定义
├── CodeMode/
│   ├── Model/                        # 生成的C#配置类
│   └── Config/                       # 生成的配置数据
```

### 12.3 配置类示例

Luban自动生成的配置类，参考 [UnitConfig.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.config/CodeMode/Model/Client/Config/ET/UnitConfig.cs)：

```csharp
public sealed partial class UnitConfig : ET.Object
{
    public UnitConfig(int Id, ET.UnitType UnitType, string Name, string HeadIcon, ET.EClassType ClassType)
    {
        this.Id = Id;
        this.UnitType = UnitType;
        this.Name = Name;
        this.HeadIcon = HeadIcon;
        this.ClassType = ClassType;
        EndInit();
    }

    public readonly int Id;
    public readonly ET.UnitType UnitType;
    public readonly string Name;
    public readonly string HeadIcon;
    public readonly ET.EClassType ClassType;
}
```

配置Category类，参考 [UnitConfigCategory.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.config/CodeMode/Model/Client/Config/ET/UnitConfigCategory.cs)：

```csharp
[ConfigProcess(ConfigType.Code)]
public partial class UnitConfigCategory : Singleton<UnitConfigCategory>, IConfig
{
    private readonly Dictionary<int, ET.UnitConfig> _dataMap;

    public UnitConfigCategory(Dictionary<int, ET.UnitConfig> dataMap)
    {
        _dataMap = dataMap;
    }

    public Dictionary<int, ET.UnitConfig> GetAll() => _dataMap;
    public ET.UnitConfig Get(int key) => _dataMap[key];
    public ET.UnitConfig GetOrDefault(int key) =>
        _dataMap.TryGetValue(key, out var v) ? v : default;
}
```

### 12.4 使用配置

```csharp
// 获取单个配置
UnitConfig config = UnitConfigCategory.Instance.Get(1001);
Log.Info($"单位名称: {config.Name}, 类型: {config.UnitType}");

// 遍历所有配置
foreach (var kv in UnitConfigCategory.Instance.GetAll())
{
    UnitConfig config = kv.Value;
    Log.Info($"ID: {kv.Key}, Name: {config.Name}");
}

// 安全获取（不存在返回null）
UnitConfig config = UnitConfigCategory.Instance.GetOrDefault(99999);
if (config != null)
{
    // 使用配置
}
```

### 12.5 导出配置

```powershell
# Unity菜单 -> ET -> Excel -> ExcelExport
# 或命令行执行Luban导出脚本
pwsh Packages/cn.etetet.config/Luban/Config/LubanGen.ps1
```

---

## 第十三章 帧同步实战

### 13.1 帧同步概述

ET提供了完善的预测回滚帧同步实现，参考 [cn.etetet.lockstep](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.lockstep) 包。

### 13.2 帧同步核心概念

- **预测帧（PredictionFrame）**：客户端预测执行的帧
- **权威帧（AuthorityFrame）**：服务器确认的帧
- **帧缓存（FrameBuffer）**：保存每帧的输入和快照
- **回滚**：预测错误时，恢复到权威帧重新执行

### 13.3 Room实体

参考 [Room.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.lockstep/Scripts/Model/Share/Room.cs)：

```csharp
[ComponentOf]
public class Room: Entity, IScene, IAwake, IUpdate
{
    public FrameBuffer FrameBuffer { get; set; }       // 帧缓存
    public FixedTimeCounter FixedTimeCounter { get; set; }  // 固定时间计数器
    public List<long> PlayerIds { get; } = new();      // 玩家列表
    public int PredictionFrame { get; set; } = -1;     // 预测帧
    public int AuthorityFrame { get; set; } = -1;      // 权威帧
    public Replay Replay { get; set; } = new();        // 录像

    private EntityRef<LSWorld> lsWorld;
    public LSWorld LSWorld
    {
        get => this.lsWorld;
        set
        {
            this.AddChild(value);
            this.lsWorld = value;
        }
    }
}
```

### 13.4 帧同步更新逻辑

参考 [RoomSystem.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.lockstep/Scripts/Hotfix/Share/RoomSystem.cs)：

```csharp
public static void Update(this Room self, OneFrameInputs oneFrameInputs)
{
    LSWorld lsWorld = self.LSWorld;

    // 设置输入到每个LSUnit
    LSUnitComponent unitComponent = lsWorld.GetComponent<LSUnitComponent>();
    foreach (var kv in oneFrameInputs.Inputs)
    {
        LSUnit lsUnit = unitComponent.GetChild<LSUnit>(kv.Key);
        LSInputComponent lsInputComponent = lsUnit.GetComponent<LSInputComponent>();
        lsInputComponent.LSInput = kv.Value;
    }

    if (!self.IsReplay)
    {
        // 保存当前帧场景数据（用于回滚）
        self.SaveLSWorld();
        self.Record(self.LSWorld.Frame);
    }

    // 执行帧逻辑
    lsWorld.Update();
}
```

### 13.5 快照保存与回滚

```csharp
// 保存LSWorld快照
private static void SaveLSWorld(this Room self)
{
    int frame = self.LSWorld.Frame;
    MemoryBuffer memoryBuffer = self.FrameBuffer.Snapshot(frame);
    memoryBuffer.Seek(0, SeekOrigin.Begin);
    memoryBuffer.SetLength(0);

    // 序列化LSWorld
    MemoryPackHelper.Serialize(self.LSWorld, memoryBuffer);

    // 计算Hash用于校验
    long hash = memoryBuffer.GetBuffer().Hash(0, (int)memoryBuffer.Length);
    self.FrameBuffer.SetHash(frame, hash);
}

// 获取指定帧的LSWorld（用于回滚）
public static LSWorld GetLSWorld(this Room self, int sceneType, int frame)
{
    MemoryBuffer memoryBuffer = self.FrameBuffer.Snapshot(frame);
    memoryBuffer.Seek(0, SeekOrigin.Begin);
    LSWorld lsWorld = MemoryPackHelper.Deserialize(typeof(LSWorld), memoryBuffer) as LSWorld;
    lsWorld.SceneType = sceneType;
    return lsWorld;
}
```

### 13.6 输入收集

```csharp
// 玩家输入
public struct LSInput
{
    public float Vx;  // X轴速度
    public float Vy;  // Y轴速度
    public bool Jump; // 跳跃
}

// 一帧所有玩家的输入
public class OneFrameInputs
{
    public Dictionary<long, LSInput> Inputs = new();
}
```

### 13.7 客户端预测更新

参考 [LSClientUpdaterSystem.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.lockstep/Scripts/Hotfix/Client/LSClientUpdaterSystem.cs)：

```csharp
// 客户端每帧预测执行
// 1. 收集本地输入
// 2. 发送给服务器
// 3. 预测执行
// 4. 收到服务器确认帧后，对比预测结果
// 5. 如果不一致，回滚到权威帧重新执行
```

---

## 第十四章 常见问题与最佳实践

### 14.1 编译问题

**问题：编译报错找不到MemoryPack**

```
解决方案：
1. 确保开启全局翻墙
2. 执行 dotnet build ET.sln 重新还原nuget包
3. 如果仍有问题，用VS打开ET.sln编译一次后再回Rider
```

**问题：Unity打开报错**

```
解决方案：
1. 确保安装了IL2CPP
2. 确保Unity版本是2022.3.62
3. 检查ProjectSettings/ProjectVersion.txt
```

### 14.2 运行问题

**问题：连接不上服务器报10037错误**

```
解决方案：
1. 检查ET/Logs目录是否有Error日志
2. 确保Unity以管理员权限运行
3. 用 netsh http delete urlacl 命令删除所有自定义urlacl
4. 检查GlobalConfig中CodeMode是否设置正确
```

**问题：服务器启动失败**

```
解决方案：
1. 必须以管理员权限运行
2. 检查端口是否被占用
3. 运行目录应该是Bin的上一层，不是Bin目录
   正确: dotnet.exe Bin/ET.App.dll --Console=1
```

### 14.3 开发最佳实践

#### 1. Entity设计原则

```csharp
// ✅ 正确：Entity只包含数据
public class Player : Entity, IAwake<string>
{
    public string Account { get; set; }
    public int Level { get; set; }
}

// ❌ 错误：Entity包含方法
public class Player : Entity
{
    public void LevelUp() { }  // 方法应该放到System中
}
```

#### 2. System编写规范

```csharp
// ✅ 正确：System以扩展方法形式编写
[EntitySystemOf(typeof(Player))]
public static partial class PlayerSystem
{
    [EntitySystem]
    private static void Awake(this Player self, string account)
    {
        self.Account = account;
    }

    public static void LevelUp(this Player self)
    {
        self.Level++;
    }
}

// ❌ 错误：在Entity中写方法
public class Player : Entity
{
    public void LevelUp()
    {
        this.Level++;
    }
}
```

#### 3. 异步安全

```csharp
// ✅ 正确：await后使用EntityRef检查
public static async ETTask DoAsync(this Player self)
{
    EntityRef<Player> playerRef = self;
    await self.GetComponent<Session>().Call(new SomeMsg());

    Player player = playerRef.Entity;
    if (player == null) return;  // Entity已销毁

    player.Level++;
}

// ❌ 错误：await后直接使用self
public static async ETTask DoAsync(this Player self)
{
    await self.GetComponent<Session>().Call(new SomeMsg());
    self.Level++;  // 可能已销毁，崩溃！
}
```

#### 4. 事件使用原则

```csharp
// ✅ 正确：跨模块通信用Publish
// 任务系统需要知道道具使用，订阅事件
[Event]
public class ItemUseEvent_QuestHandler : AEvent<Scene, ItemUseEvent>
{
    public override async ETTask Handle(Scene scene, ItemUseEvent args)
    {
        // 任务系统处理道具使用事件
        await ETTask.CompletedTask;
    }
}

// ✅ 正确：同模块通信用Invoke
// TimerComponent回调，属于同一模块
EventSystem.Instance.Invoke(TimerType.SomeTimer, new TimerCallback { Args = entity });
```

#### 5. 包依赖规范

```
# ✅ 正确：单向依赖
cn.etetet.map 依赖 cn.etetet.unit (高层依赖低层)

# ❌ 错误：相互依赖
cn.etetet.map 依赖 cn.etetet.unit
cn.etetet.unit 依赖 cn.etetet.map  (违反单向依赖原则！)
```

#### 6. 编译规范

```powershell
# 项目只有一个编译命令
dotnet build ET.sln

# 不要使用其他编译方式
# ❌ 不要单独编译某个csproj
# ❌ 不要在Unity中按F6编译（打包时除外）
```

### 14.4 调试技巧

#### 1. Entity可视化

开启 `ENABLE_VIEW` 宏后，在Unity Hierarchy面板可以看到所有Entity对象：

```
Init/Global/Scene(Process)
├── Player (account001)
│   ├── MoveComponent
│   ├── ItemsComponent
│   │   ├── Item (1001)
│   │   └── Item (1002)
│   └── SpellComponent
└── NPC (npc001)
    └── MoveComponent
```

#### 2. 日志查看

```csharp
// ET自带日志系统，会自动带上Scene名
Log.Debug("调试信息");
Log.Info("普通信息");
Log.Warning("警告信息");
Log.Error("错误信息");

// 日志输出到 Logs/ 目录
```

#### 3. REPL模式

服务端支持REPL，可以在控制台输入 `repl` 进入REPL模式，动态执行代码：

```
> repl
> Game.Scene.GetComponent<PlayerComponent>().Get(1001).Level
10
```

### 14.5 性能优化

#### 1. 使用对象池

```csharp
// ✅ 使用ListComponent代替List
using ListComponent<int> list = ListComponent<int>.Create();
list.Add(1);
list.Add(2);
// using结束自动回收

// ✅ Entity使用对象池
Player player = scene.AddChild<Player>(true);  // true表示使用对象池
```

#### 2. 使用EntityRef

```csharp
// ✅ 使用EntityRef避免强引用导致Entity无法回收
private EntityRef<Player> playerRef;

// ❌ 直接持有Entity引用会阻止GC
private Player player;
```

#### 3. 事件使用struct

```csharp
// ✅ 事件用struct，0GC
public struct PlayerLevelUpEvent
{
    public long PlayerId;
    public int NewLevel;
}

// ❌ 事件用class会产生GC
public class PlayerLevelUpEvent  // 不要用class
{
}
```

---

## 附录

### A. 关键文件索引

| 文件 | 说明 |
|------|------|
| [Entity.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Entity/Entity.cs) | Entity基类定义 |
| [EventSystem.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/World/EventSystem/EventSystem.cs) | 事件系统 |
| [ETTask.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/ETTask/ETTask.cs) | 异步系统 |
| [Fiber.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/World/Fiber/Fiber.cs) | 纤程系统 |
| [World.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/World/World.cs) | World单例管理 |
| [TimerComponent.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Timer/TimerComponent.cs) | 定时器组件 |
| [Scene.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Scripts/Core/Share/Entity/Scene.cs) | Scene定义 |
| [Room.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.lockstep/Scripts/Model/Share/Room.cs) | 帧同步房间 |
| [RoomSystem.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.lockstep/Scripts/Hotfix/Share/RoomSystem.cs) | 帧同步房间逻辑 |

### B. Book文档索引

| 文档 | 说明 |
|------|------|
| [1.1运行指南.md](file:///d:/Unity/LockstepDemo/ET/Book/1.1运行指南.md) | 环境搭建与运行 |
| [2.1CSharp的协程.md](file:///d:/Unity/LockstepDemo/ET/Book/2.1CSharp的协程.md) | 协程基础 |
| [2.2更好的协程.md](file:///d:/Unity/LockstepDemo/ET/Book/2.2更好的协程.md) | ETTask设计 |
| [2.3单线程异步.md](file:///d:/Unity/LockstepDemo/ET/Book/2.3单线程异步.md) | 单线程异步 |
| [3.3一切皆实体.md](file:///d:/Unity/LockstepDemo/ET/Book/3.3一切皆实体.md) | Entity设计理念 |
| [3.4事件机制EventSystem.md](file:///d:/Unity/LockstepDemo/ET/Book/3.4事件机制EventSystem.md) | 事件系统 |
| [4.1组件式设计.md](file:///d:/Unity/LockstepDemo/ET/Book/4.1组件式设计.md) | 组件设计 |
| [5.4Actor模型.md](file:///d:/Unity/LockstepDemo/ET/Book/5.4Actor模型.md) | Actor模型 |
| [5.5Actor Location-ZH.md](file:///d:/Unity/LockstepDemo/ET/Book/5.5Actor Location-ZH.md) | Actor定位 |
| [6.1AI框架.md](file:///d:/Unity/LockstepDemo/ET/Book/6.1AI框架.md) | AI框架 |
| [8.1ET Package制作指南.md](file:///d:/Unity/LockstepDemo/ET/Book/8.1ET Package制作指南.md) | Package制作 |

### C. 学习路线建议

1. **第一阶段**：环境搭建 -> 运行Demo -> 理解ECS架构
2. **第二阶段**：Entity/Component -> System -> EventSystem
3. **第三阶段**：ETTask异步 -> Timer定时器 -> Fiber纤程
4. **第四阶段**：网络系统 -> Actor模型 -> 配置系统
5. **第五阶段**：帧同步实战 -> 状态同步 -> AI框架
6. **第六阶段**：Package制作 -> 热更新 -> 性能优化

---

> **结语**：ET框架的设计理念是"数据与逻辑分离"，理解这一点是掌握ET的关键。建议新手从修改Demo开始，逐步理解每个系统的设计思路。遇到问题多看源码和Book文档，ET的源码就是最好的教程。
