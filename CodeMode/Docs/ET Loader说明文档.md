# ET Loader 运行时加载方案说明文档

> 本文档详细说明 ET 框架 Loader 运行时加载机制的核心原理、迁移实施步骤、技术实现细节和日常使用方法。
> 适用于 `d:\Unity\LockstepDemo\CodeMode` 项目，基于 ET 2.0.7 版本。

---

## 目录

1. [ET Loader 的核心作用和工作原理](#1-et-loader-的核心作用和工作原理)
2. [项目架构与文件结构](#2-项目架构与文件结构)
3. [迁移实施的具体步骤和操作指南](#3-迁移实施的具体步骤和操作指南)
4. [技术实现细节和关键代码解析](#4-技术实现细节和关键代码解析)
5. [日常使用方法](#5-日常使用方法)
6. [最佳实践](#6-最佳实践)
7. [常见问题解答](#7-常见问题解答)
8. [附录：依赖包清单与版本](#8-附录依赖包清单与版本)

---

## 1. ET Loader 的核心作用和工作原理

### 1.1 核心作用

ET Loader 是 ET 框架的**运行时加载器**，负责在 Unity 启动时完成以下工作：

1. **加载全局配置** - 读取 `GlobalConfig.asset` 获取 CodeMode、SceneName 等配置
2. **注册基础单例** - 初始化 Logger / TimeInfo / FiberManager 等核心系统
3. **加载资源系统** - 通过 ResourcesComponent 初始化 YooAsset 资源包
4. **动态加载程序集** - 加载 Model / ModelView / Hotfix / HotfixView 程序集
5. **触发业务入口** - 通过反射调用 `ET.Entry.Start()` 启动业务逻辑

### 1.2 工作原理（启动流程图）

```
Unity Play
  │
  ▼
GameInit.Start() (Assembly-CSharp.dll)
  │  [Assets/Scripts/GameInit.cs]
  │
  ├─ 1. Resources.Load<GlobalConfig>("GlobalConfig")
  │     读取全局配置（CodeMode/SceneName/Address）
  │
  ├─ 2. Parser.Default.ParseArguments<Options>(args)
  │     解析命令行参数到 Options 单例
  │
  ├─ 3. World.Instance.AddSingleton<Logger>()
  │     .Log = new UnityLogger("None")
  │     注册日志系统（输出到 Unity Console）
  │
  ├─ 4. World.Instance.AddSingleton<TimeInfo>()
  │     World.Instance.AddSingleton<FiberManager>()
  │     注册时间信息和 Fiber 管理器（多线程调度）
  │
  ├─ 5. await ResourcesComponent.CreatePackageAsync("DefaultPackage")
  │     加载 YooAsset 资源包（失败不阻塞，学习项目可忽略）
  │
  └─ 6. CodeLoader.Start()
        │  [cn.etetet.loader/Scripts/Loader/Client/CodeLoader.cs]
        │
        ├─ DownloadAsync()
        │   编辑器模式：跳过（dll 已由 Unity 编译加载）
        │   运行时模式：从资源包加载 dll 字节
        │
        ├─ 扫描 AppDomain 程序集
        │   收集 ET.Core / ET.Loader / ET.Model / ET.ModelView 等
        │
        ├─ LoadHotfix()
        │   编辑器模式：从 AppDomain 获取已加载的 ET.Hotfix / ET.HotfixView
        │   运行时模式：Assembly.Load(dll字节) 动态加载
        │
        ├─ World.Instance.AddSingleton<CodeTypes, Assembly[]>(assemblies)
        │   注册代码类型系统（扫描特性标注的类型）
        │
        └─ IStaticMethod start = new StaticMethod(hotfixAssembly, "ET.Entry", "Start")
           start.Run()
              │
              ▼
           ET.Entry.Start() (ET.Hotfix.dll)
              │  [cn.codemode.helloworld/Scripts/Hotfix/Share/Entry.cs]
              │
              ├─ await FiberManager.Instance.CreateMainFiber(1, "GameScene")
              │   创建主 Fiber（内部创建 Root Scene）
              │
              ├─ 反射获取 FiberManager.mainFiber → Fiber.Root → Scene
              │
              ├─ scene.AddComponent<PlayerComponent>()
              │   添加逻辑组件（Awake 自动添加 Move/Jump）
              │
              └─ 反射调用 ET.Client.MainSceneViewInit.Init(player)
                    │  [cn.codemode.helloworld/Scripts/HotfixView/Client/MainSceneViewInit.cs]
                    │
                    ├─ CreateSceneContent()
                    │   创建 Unity 物体（地面/Player/Camera）
                    │
                    └─ InjectPlayerView(player)
                        添加视图组件并注入 Unity 引用
```

### 1.3 与简化方案的关键区别

| 维度 | 简化方案（已弃用） | Loader 方案（当前） |
| --- | --- | --- |
| Unity 入口 | GameEntry.cs（自写） | GameInit.cs（基于 ET 原版 Init.cs） |
| 业务入口 | GameEntry.CreatePlayer() | ET.Entry.Start()（Hotfix 层） |
| 框架初始化 | 手动添加 7 个单例 | 使用 ET 原版链（Logger/Resources/CodeLoader） |
| Hotfix 加载 | Unity 编译期硬编码 | CodeLoader 动态加载（运行时可热更） |
| 资源系统 | 无 | ResourcesComponent + YooAssets |
| 日志系统 | Debug.Log | ET Logger（分级别、可热更） |
| 扩展性 | 受限 | 强（符合 ET 规范，可接入网络/配置等） |

---

## 2. 项目架构与文件结构

### 2.1 整体架构

```
d:\Unity\LockstepDemo\CodeMode\
├─ Assets/
│  ├─ Resources/
│  │  └─ GlobalConfig.asset          # 全局配置（CodeMode/SceneName/Address）
│  ├─ Scripts/
│  │  └─ GameInit.cs                 # Unity MonoBehaviour 入口
│  └─ Scenes/
│     └─ README.md                   # 场景创建说明
│
├─ Packages/
│  ├─ manifest.json                  # 包依赖清单（含 10 个 ET 包）
│  │
│  ├─ cn.codemode.demo/              # CodeMode 配置包
│  │  └─ Runtime/
│  │     ├─ Model/ET.Model.asmdef       (references: ET.Core)
│  │     ├─ Hotfix/ET.Hotfix.asmdef     (references: ET.Core, ET.Model; noEngineReferences:true)
│  │     ├─ ModelView/ET.ModelView.asmdef (references: ET.Core, ET.Model)
│  │     ├─ HotfixView/ET.HotfixView.asmdef (references: ET.Core, ET.Model, ET.ModelView, ET.Hotfix)
│  │     ├─ Editor/CodeModeMenu.cs      # ET 菜单扩展
│  │     └─ Config/ET.Config.asmdef
│  │
│  └─ cn.codemode.helloworld/        # 业务代码包
│     └─ Scripts/
│        ├─ Model/Share/             # 数据层（不热更，可引用 UnityEngine）
│        │  ├─ PlayerComponent.cs
│        │  ├─ MoveComponent.cs
│        │  ├─ JumpComponent.cs
│        │  └─ InputRequest.cs
│        ├─ Hotfix/Share/            # 逻辑层（可热更，不能引用 UnityEngine）
│        │  ├─ Entry.cs              # ★ ET.Entry.Start 业务入口
│        │  ├─ PlayerComponentSystem.cs
│        │  └─ JumpSystem.cs
│        ├─ ModelView/Client/        # 表现数据层（不热更，可引用 UnityEngine）
│        │  ├─ PlayerViewComponent.cs
│        │  ├─ PlayerAnimatorComponent.cs
│        │  └─ InputComponent.cs
│        └─ HotfixView/Client/       # 表现逻辑层（可热更，可引用 UnityEngine）
│           ├─ MainSceneViewInit.cs  # ★ 视图初始化（创建 Unity 物体）
│           ├─ PlayerViewSystem.cs   # 核心驱动系统
│           ├─ MoveSystem.cs
│           ├─ InputSystem.cs
│           └─ PlayerAnimatorSystem.cs
│
└─ Docs/
   ├─ ET Loader说明文档.md          # 本文档
   └─ 角色控制模块使用说明书.md      # 角色控制模块文档
```

### 2.2 程序集依赖关系

```
Assembly-CSharp (GameInit.cs)
  ↓ autoReferenced
ET.Core (FiberManager/Scene/Entity/Logger)
ET.Init (GlobalConfig/CodeMode/BuildType)
ET.Loader (CodeLoader/UnityLogger/Init.cs)
ET.YooAssets (ResourcesComponent)
ET.HybridCLR (RuntimeApi，运行时热更支持)

ET.Model (业务数据层)
  ↓ references
ET.Core

ET.Hotfix (业务逻辑层，noEngineReferences:true)
  ↓ references
ET.Core, ET.Model

ET.ModelView (表现数据层)
  ↓ references
ET.Core, ET.Model

ET.HotfixView (表现逻辑层)
  ↓ references
ET.Core, ET.Model, ET.ModelView, ET.Hotfix
```

### 2.3 noEngineReferences 约束

| 程序集 | noEngineReferences | 可否 using UnityEngine | 说明 |
| --- | --- | --- | --- |
| ET.Model | false | ✅ 可以 | 数据层可用 Vector3 等 |
| ET.Hotfix | **true** | ❌ 不能 | 逻辑层纯 C#，便于热更 |
| ET.ModelView | false | ✅ 可以 | 表现数据持有 Transform 等 |
| ET.HotfixView | false | ✅ 可以 | 表现逻辑可调用 Unity API |

> **关键**：ET.Hotfix 是唯一不能引用 UnityEngine 的层，因为它是热更核心，必须与 Unity 解耦。

---

## 3. 迁移实施的具体步骤和操作指南

### 3.1 迁移前准备（已完成）

迁移前项目状态：
- 使用自写的 `GameEntry.cs` 直接初始化 ET 框架
- 已引用 cn.etetet.loader 等 9 个 ET 包，但未使用其 Init/CodeLoader
- 缺少 cn.etetet.hybridclr 包（CodeLoader 依赖）
- asmdef 配置错误（自引用）

### 3.2 迁移步骤详解

#### 步骤 1：添加 cn.etetet.hybridclr 包依赖

修改 `Packages/manifest.json`，添加：

```json
"cn.etetet.hybridclr": "file:../../ET/Packages/cn.etetet.hybridclr"
```

**作用**：提供 `ET.HybridCLR` asmdef，被 `ET.Loader.asmdef` 引用。包含 `RuntimeApi.cs`（运行时加载 AOT dll 的接口）。

#### 步骤 2：修复 asmdef 引用配置

修改 `Packages/cn.codemode.demo/Runtime/` 下的 4 个 asmdef：

| asmdef | 修复前 | 修复后 |
| --- | --- | --- |
| ET.Model.asmdef | `["ET.Model"]` (自引用) | `["ET.Core"]` |
| ET.Hotfix.asmdef | `["ET.Hotfix", "ET.Model"]` (自引用) | `["ET.Core", "ET.Model"]` |
| ET.ModelView.asmdef | `["ET.ModelView", "ET.Model"]` (自引用) | `["ET.Core", "ET.Model"]` |
| ET.HotfixView.asmdef | `["ET.HotfixView", "ET.ModelView", "ET.Hotfix"]` (自引用) | `["ET.Core", "ET.Model", "ET.ModelView", "ET.Hotfix"]` |

**原因**：asmdef 的 references 不能自引用。原配置会导致编译错误（找不到 FiberManager/Scene 等 ET.Core 类型）。

#### 步骤 3：创建 GameInit.cs（Unity 入口）

在 `Assets/Scripts/GameInit.cs` 创建 MonoBehaviour，结构参考 ET 原版 `Init.cs`：

```csharp
public class GameInit : MonoBehaviour
{
    private async ETTask StartAsync()
    {
        // 1. 加载 GlobalConfig
        GlobalConfig globalConfig = Resources.Load<GlobalConfig>("GlobalConfig");

        // 2. 注册 Options（命令行参数）
        World.Instance.AddSingleton<Options>(parsedOptions);

        // 3. 注册日志
        World.Instance.AddSingleton<Logger>().Log = new UnityLogger("None");

        // 4. 注册时间信息和 Fiber 管理器
        World.Instance.AddSingleton<TimeInfo>();
        World.Instance.AddSingleton<FiberManager>();

        // 5. 加载资源包（容错处理）
        try {
            await World.Instance.AddSingleton<ResourcesComponent>()
                .CreatePackageAsync("DefaultPackage", true);
        } catch (Exception e) {
            Debug.LogWarning($"ResourcesComponent failed: {e.Message}");
        }

        // 6. 启动 CodeLoader（反射调用 ET.Entry.Start）
        World.Instance.AddSingleton<CodeLoader>().Start().Coroutine();
    }
}
```

**类名用 GameInit 而非 Init 的原因**：避免与 cn.etetet.loader 包自带的 `ET.Client.Init` 类冲突。

#### 步骤 4：创建 GlobalConfig.asset

在 `Assets/Resources/GlobalConfig.asset` 创建配置资源：

```yaml
# CodeMode: 1=Client, 2=Server, 3=ClientServer
# SceneName: 业务场景名（用于 FiberInit 事件订阅）
# Address: 服务端地址（网络模块使用）
CodeMode: 1
SceneName: GameScene
Address: 127.0.0.1:10101
EditorScripts: 1
```

**创建方式**：在 Unity Project 窗口右键 → Create → ET → CreateGlobalConfig，保存到 `Assets/Resources/`。

#### 步骤 5：创建 Entry.cs（Hotfix 层业务入口）

在 `Packages/cn.codemode.helloworld/Scripts/Hotfix/Share/Entry.cs`：

```csharp
public static class Entry
{
    public static void Start()
    {
        StartAsync().Coroutine();
    }

    private static async ETTask StartAsync()
    {
        // 1. 创建主 Fiber
        await FiberManager.Instance.CreateMainFiber(1, "GameScene");

        // 2. 反射获取 Scene
        Scene scene = GetMainScene();

        // 3. 添加逻辑组件
        PlayerComponent player = scene.AddComponent<PlayerComponent>();
        player.PlayerId = 1001;

        // 4. 反射调用 HotfixView 层视图初始化
        Type viewType = Type.GetType("ET.Client.MainSceneViewInit, ET.HotfixView");
        viewType?.GetMethod("Init")?.Invoke(null, new object[] { player });
    }
}
```

**被调用方式**：CodeLoader 通过反射调用：
```csharp
IStaticMethod start = new StaticMethod(hotfixAssembly, "ET.Entry", "Start");
start.Run();
```

#### 步骤 6：创建 MainSceneViewInit.cs（HotfixView 层视图初始化）

在 `Packages/cn.codemode.helloworld/Scripts/HotfixView/Client/MainSceneViewInit.cs`：

```csharp
public static class MainSceneViewInit
{
    public static void Init(PlayerComponent player)
    {
        CreateSceneContent();      // 创建地面/Player/Camera
        InjectPlayerView(player);  // 添加视图组件并注入 Unity 引用
    }
}
```

#### 步骤 7：删除旧的 GameEntry.cs

删除 `Assets/Scripts/GameEntry.cs` 和 `PlayerBootstrap.cs`，它们的功能已被 GameInit.cs + Entry.cs + MainSceneViewInit.cs 取代。

### 3.3 迁移验证清单

- [x] manifest.json 包含 10 个 ET 包（含 cn.etetet.hybridclr）
- [x] 4 个 asmdef 引用关系正确（无自引用）
- [x] noEngineReferences 约束：Hotfix=true，其他=false
- [x] GameInit.cs 在 Assets/Scripts/（Assembly-CSharp）
- [x] Entry.cs 在 Hotfix/Share/（ET.Hotfix 程序集）
- [x] MainSceneViewInit.cs 在 HotfixView/Client/（ET.HotfixView 程序集）
- [x] GlobalConfig.asset 在 Assets/Resources/
- [x] 旧的 GameEntry.cs 已删除

---

## 4. 技术实现细节和关键代码解析

### 4.1 CodeLoader 的双模式加载机制

CodeLoader 是 Loader 方案的核心，支持两种加载模式：

#### 编辑器模式（UNITY_EDITOR）

```csharp
// CodeLoader.Start() 编辑器模式逻辑
HashSet<string> assemblyNames = new() {
    "ET.Core", "ET.Loader",
    "ET.Model", "ET.ModelView", "ET.Editor"  // 编辑器额外包含
};

// 直接从 AppDomain 获取已加载的程序集
foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
{
    if (assemblyNames.Contains(assembly.GetName().Name))
        assemblies.Add(assembly);
}

// Hotfix 也从 AppDomain 获取（Unity 编译期已加载）
// hotfixAssembly = AppDomain 中的 "ET.Hotfix"
// hotfixViewAssembly = AppDomain 中的 "ET.HotfixView"
```

**特点**：编辑器模式下不加载 dll，直接使用 Unity 编译的程序集，修改代码后 Unity 自动重编译。

#### 运行时模式（!UNITY_EDITOR）

```csharp
// 从资源包加载 dll 字节
this.dlls = new Dictionary<string, TextAsset> {
    ["ET.Model.dll"] = await ResourcesComponent.Instance.LoadAssetAsync<TextAsset>("ET.Model.dll"),
    ["ET.Model.pdb"] = await ResourcesComponent.Instance.LoadAssetAsync<TextAsset>("ET.Model.pdb"),
    // ... ModelView/Hotfix/HotfixView
};

// HybridCLR 加载 AOT dll（元数据补充）
foreach (var kv in this.aotDlls) {
    HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(
        kv.Value.bytes, HybridCLR.HomologousImageMode.SuperSet);
}

// 动态加载程序集
Assembly modelAssembly = Assembly.Load(modelAssBytes, modelPdbBytes);
Assembly hotfixAssembly = Assembly.Load(hotfixAssBytes, hotfixPdbBytes);
```

**特点**：运行时从资源包加载 dll 字节，通过 `Assembly.Load` 动态加载。HybridCLR 提供元数据补充，使热更 dll 可调用 AOT dll 的类型。

### 4.2 热更新机制（CodeLoader.Reload）

```csharp
public void Reload()
{
    // 重新加载 Hotfix dll（isReload=true）
    (Assembly hotfixAssembly, Assembly hotfixViewAssembly) = this.LoadHotfix(true);

    // 重新注册 CodeTypes
    List<Assembly> list = new(this.assemblies);
    list.Add(hotfixViewAssembly);
    list.Add(hotfixAssembly);
    CodeTypes codeTypes = World.Instance.AddSingleton<CodeTypes, Assembly[]>(list.ToArray());
    codeTypes.CodeProcess();

    Log.Info($"reload dll finish!");
}
```

**热更流程**：
1. 修改 Hotfix 层代码
2. 编译生成新的 ET.Hotfix.dll
3. 替换 `Packages/cn.etetet.loader/Bundles/Code/ET.Hotfix.dll.bytes`
4. 调用 `CodeLoader.Instance.Reload()` 重新加载
5. 新逻辑立即生效，无需重启 Unity

### 4.3 ET.Entry.Start 的反射调用

CodeLoader 通过 `IStaticMethod` 反射调用业务入口：

```csharp
// CodeLoader.Start() 最后一步
IStaticMethod start = new StaticMethod(hotfixAssembly, "ET.Entry", "Start");
start.Run();
```

**IStaticMethod 实现**：内部用 `MethodInfo.Invoke` 调用 `ET.Entry.Start()` 静态方法。

**设计意图**：业务入口在 Hotfix 层（可热更），Loader 层通过反射调用，解耦了加载器和业务代码。

### 4.4 跨层反射调用（Entry → MainSceneViewInit）

由于 asmdef 引用方向是 `HotfixView → Hotfix`（不能反过来），Hotfix 层的 Entry.cs 无法直接调用 HotfixView 层的 MainSceneViewInit。

**解决方案**：使用反射跨层调用：

```csharp
// Entry.cs (Hotfix层)
Type viewType = Type.GetType("ET.Client.MainSceneViewInit, ET.HotfixView");
MethodInfo initMethod = viewType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
initMethod?.Invoke(null, new object[] { player });
```

**ET 原版方案**：使用 `[Invoke]` 特性订阅 FiberInit 事件，更优雅但复杂。学习项目用反射简化。

### 4.5 FiberManager.mainFiber 的反射获取

`FiberManager.mainFiber` 是 private 字段，外部无法直接访问。`Fiber.Instance` 是 internal static，跨程序集无法访问。

**解决方案**：通过反射获取：

```csharp
// Entry.cs
private static Scene GetMainScene()
{
    FieldInfo field = typeof(FiberManager).GetField("mainFiber",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Fiber fiber = field?.GetValue(FiberManager.Instance) as Fiber;
    return fiber?.Root;  // Fiber.Root 是 public
}
```

**ET 原版方案**：通过 `[Invoke(sceneType)]` 订阅 FiberInit 事件，在事件参数中获取 Fiber。学习项目用反射简化。

---

## 5. 日常使用方法

### 5.1 运行项目

#### 5.1.1 创建 Unity 场景

1. 打开 Unity Editor，加载 `d:\Unity\LockstepDemo\CodeMode` 项目
2. 等待自动编译完成（首次会编译所有 ET 包，耗时较长）
3. 创建新场景：File → New Scene → Empty Scene
4. 在 Hierarchy 中创建空 GameObject，命名为 "GameInit"
5. 选中 GameInit 对象，Inspector → Add Component → 输入 "GameInit" → 选中
6. 保存场景为 `Assets/Scenes/Main.unity`

#### 5.1.2 运行测试

1. 打开 `Main.unity` 场景
2. 点击 Play 按钮
3. 观察 Console 输出：
   - `[GameInit] ET framework initialized` - 框架启动成功
   - `[Entry] Business init finished` - 业务初始化完成
   - `[MainSceneViewInit] Player view injected` - 视图注入完成
4. 操作验证：
   - WASD 或方向键控制 Player 移动
   - 空格键控制 Player 跳跃
   - 落地后才能再次跳跃

### 5.2 修改业务代码

#### 5.2.1 修改 Hotfix 层代码（可热更）

例如修改跳跃力度：

1. 编辑 `Packages/cn.codemode.helloworld/Scripts/Hotfix/Share/JumpSystem.cs`
2. 修改 `Awake` 方法中的默认值：
   ```csharp
   self.JumpForce = 8f;  // 原为 6f
   ```
3. Unity 会自动重编译 ET.Hotfix 程序集
4. 点击 Play，新的跳跃力度生效

#### 5.2.2 修改 Model 层代码（不可热更）

例如修改 MoveComponent 的字段：

1. 编辑 `Packages/cn.codemode.helloworld/Scripts/Model/Share/MoveComponent.cs`
2. 添加新字段：
   ```csharp
   public float RunSpeedMultiplier { get; set; } = 2f;
   ```
3. Unity 重编译 ET.Model 程序集
4. **注意**：Model 层修改不能热更，正式发布需要重新打包客户端

### 5.3 添加新组件

#### 5.3.1 添加逻辑组件（Hotfix 层）

1. 在 `Scripts/Model/Share/` 创建数据组件：
   ```csharp
   [ComponentOf(typeof(PlayerComponent))]
   public class SkillComponent : Entity, IAwake
   {
       public int SkillId { get; set; }
   }
   ```

2. 在 `Scripts/Hotfix/Share/` 创建逻辑系统：
   ```csharp
   [EntitySystemOf(typeof(SkillComponent))]
   [FriendOf(typeof(SkillComponent))]
   public static partial class SkillSystem
   {
       [EntitySystem]
       private static void Awake(this SkillComponent self)
       {
           self.SkillId = 1001;
       }
   }
   ```

3. 在 PlayerComponentSystem.Awake 中添加：
   ```csharp
   self.AddComponent<SkillComponent>();
   ```

#### 5.3.2 添加视图组件（HotfixView 层）

1. 在 `Scripts/ModelView/Client/` 创建视图组件：
   ```csharp
   [ComponentOf(typeof(PlayerComponent))]
   public class SkillEffectComponent : Entity, IAwake
   {
       public GameObject Effect { get; set; }
   }
   ```

2. 在 `Scripts/HotfixView/Client/` 创建表现系统：
   ```csharp
   [EntitySystemOf(typeof(SkillEffectComponent))]
   public static partial class SkillEffectSystem
   {
       [EntitySystem]
       private static void Awake(this SkillEffectComponent self)
       {
           self.Effect = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("SkillEffect"));
       }
   }
   ```

3. 在 MainSceneViewInit.InjectPlayerView 中添加：
   ```csharp
   player.AddComponent<SkillEffectComponent>();
   ```

### 5.4 热更新测试（编辑器模式）

1. 启动 Play 模式
2. 修改 `Scripts/Hotfix/Share/JumpSystem.cs` 中的 Gravity 值
3. Unity 编辑器外编译新的 ET.Hotfix.dll（用 dotnet build）
4. 替换 `Packages/cn.etetet.loader/Bundles/Code/ET.Hotfix.dll.bytes`
5. 在 Console 执行：`CodeLoader.Instance.Reload()`
6. 新的 Gravity 值立即生效（无需重启 Play）

> **注意**：编辑器模式下默认不加载 dll.bytes，需要修改 CodeLoader 启用 isReload 模式。详见 [7.3](#73-编辑器热更不生效)。

---

## 6. 最佳实践

### 6.1 代码分层原则

| 代码类型 | 放置位置 | 示例 |
| --- | --- | --- |
| 纯数据（无 Unity 类型） | Model/Share/ | PlayerComponent, JumpComponent |
| 纯数据（含 Unity 类型） | Model/Share/ 或 ModelView/Client/ | MoveComponent（含 Vector3） |
| 纯逻辑（无 Unity API） | Hotfix/Share/ | JumpSystem（float 计算） |
| 表现逻辑（含 Unity API） | HotfixView/Client/ | MoveSystem（Vector3.MoveTowards） |
| 视图组件（持有 Unity 引用） | ModelView/Client/ | PlayerViewComponent（Transform） |

### 6.2 跨层调用原则

- ✅ HotfixView 可以调用 Hotfix（asmdef 引用方向）
- ✅ Hotfix 可以调用 Model（asmdef 引用方向）
- ❌ Hotfix 不能直接调用 HotfixView（反向引用）
- ✅ Hotfix 通过反射调用 HotfixView（Entry.cs → MainSceneViewInit）
- ✅ ET 原版用 [Invoke]/[Event] 事件系统解耦（更优雅但复杂）

### 6.3 noEngineReferences 约束

- ET.Hotfix 层 `noEngineReferences: true`，**不能** using UnityEngine
- 如果 Hotfix 层代码需要 Vector3，改用 float x/y/z 三个字段
- 如果必须用 UnityEngine API，将代码移到 HotfixView 层

### 6.4 单例注册顺序

ET 框架的初始化顺序很重要，不能随意调整：

```
Options → Logger → TimeInfo → FiberManager → ResourcesComponent → CodeLoader
```

**原因**：
- Logger 依赖 Options（读取 SingleThread 配置）
- FiberManager 依赖 TimeInfo（调度使用时间）
- CodeLoader 依赖 FiberManager（创建主 Fiber）

### 6.5 资源系统容错

学习项目可能未配置 YooAsset 资源服务器，GameInit.cs 对资源系统失败做了 try/catch：

```csharp
try {
    await World.Instance.AddSingleton<ResourcesComponent>()
        .CreatePackageAsync("DefaultPackage", true);
} catch (Exception e) {
    Debug.LogWarning($"ResourcesComponent failed: {e.Message}");
}
```

正式项目应配置 YooAsset 资源服务器，移除 try/catch 让资源系统失败阻塞启动。

---

## 7. 常见问题解答

### 7.1 编译错误：找不到 FiberManager / Scene / Entity

**原因**：asmdef 引用配置错误，未引用 ET.Core。

**解决**：检查 `Packages/cn.codemode.demo/Runtime/Hotfix/ET.Hotfix.asmdef`，确保 references 包含 `"ET.Core"`。

### 7.2 编译错误：CS0246 UnityEngine could not be found

**原因**：在 Hotfix 层（noEngineReferences:true）using UnityEngine。

**解决**：
1. 将代码移到 HotfixView 层（noEngineReferences:false）
2. 或移除 using UnityEngine，改用 float/int/bool 纯计算

### 7.3 编辑器热更不生效

**原因**：编辑器模式下 CodeLoader 从 AppDomain 获取已加载程序集，不会读取 dll.bytes。

**解决**：调用 `CodeLoader.Instance.Reload()` 会强制从 `Packages/cn.etetet.loader/Bundles/Code/` 读取 dll.bytes 重新加载。

### 7.4 运行时 ResourcesComponent 报错

**原因**：未配置 YooAsset 资源服务器，`Resources.Load<YooConfig>("YooConfig")` 失败或 `CreatePackageAsync` 抛异常。

**解决**：
- 学习项目：忽略警告（GameInit.cs 已 try/catch）
- 正式项目：配置 YooAsset 资源服务器，参考 [YooAsset 文档](https://www.yooasset.com/)

### 7.5 GlobalConfig 报错：未找到资源

**原因**：`Assets/Resources/GlobalConfig.asset` 不存在。

**解决**：
1. 在 Unity Project 窗口，右键 `Assets/Resources/` 文件夹
2. Create → ET → CreateGlobalConfig
3. 命名为 `GlobalConfig`
4. 设置 CodeMode=1, SceneName=GameScene

### 7.6 Play 后 Console 无任何输出

**原因**：场景中未挂载 GameInit 组件。

**解决**：
1. 打开 Main.unity 场景
2. 创建空 GameObject，命名为 "GameInit"
3. Add Component → 输入 "GameInit" → 选中
4. 保存场景，点击 Play

### 7.7 Player 不会移动

**原因**：InputComponent 未被添加，或 PlayerViewSystem 未驱动。

**解决**：
1. 检查 MainSceneViewInit.InjectPlayerView 是否添加了 InputComponent
2. 检查 Console 是否有 `[MainSceneViewInit] Player view injected` 日志
3. 检查 PlayerViewSystem.Update 是否被调用（FiberManager.Update 驱动）

### 7.8 跳跃后不能再次跳跃

**原因**：地面检测失败，IsGrounded 一直为 false。

**解决**：
1. 检查 CharacterController 是否添加（MainSceneViewInit.CreateSceneContent）
2. 检查 PlayerViewSystem 的地面检测逻辑（`controller.isGrounded`）
3. 调整 GroundY 值（PlayerViewComponent.GroundY = 1f）

### 7.9 编译警告：AssemblyReference.asmref 重复

**原因**：CodeMode 切换后，多个目录的 asmref 同时存在。

**解决**：运行 ET 菜单 → Verify CodeMode，或手动检查 `Scripts/*/Client/` 和 `Scripts/*/Server/` 的 asmref 文件，确保只有当前模式需要的目录有 asmref。

---

## 8. 附录：依赖包清单与版本

### 8.1 项目引用的 10 个 ET 包

| 包名 | 版本 | 功能 |
| --- | --- | --- |
| cn.etetet.core | 2.0.8 | ET 核心（Entity/Scene/Fiber/EventSystem） |
| cn.etetet.loader | 2.0.7 | 加载器（CodeLoader/Init/UnityLogger） |
| cn.etetet.hybridclr | 8.5.1 | HybridCLR 集成（运行时热更 AOT dll） |
| cn.etetet.memorypack | - | 高性能序列化（帧同步快照） |
| cn.etetet.proto | - | Proto 消息定义（跨网络 RPC） |
| cn.etetet.sourcegenerator | - | 编译期代码生成（[EntitySystemOf] 等） |
| cn.etetet.startconfig | 2.0.1 | 启动配置 |
| cn.etetet.config | - | 配置系统 |
| cn.etetet.yooassets | 2.3.14 | YooAsset 资源管理（ResourcesComponent） |
| com.etetet.init | 0.0.1 | 初始化（GlobalConfig/CodeMode 枚举） |

### 8.2 包依赖关系

```
cn.etetet.loader (2.0.7)
  ├─ cn.etetet.core (2.0.8)
  ├─ cn.etetet.startconfig (2.0.1)
  └─ cn.etetet.yooassets (2.3.14)
       └─ cn.etetet.core (1.0.0+)

cn.etetet.hybridclr (8.5.1)
  └─ 无 ET 包依赖（独立的 HybridCLR Unity Package 封装）

com.etetet.init (0.0.1)
  └─ 无 ET 包依赖（提供 GlobalConfig/CodeMode 定义）
```

### 8.3 Unity 版本要求

- Unity 团结引擎 2022.3.62t10+
- TuanjieEditorVersion 1.9.3+

### 8.4 关键文件索引

| 文件 | 路径 | 作用 |
| --- | --- | --- |
| GameInit.cs | Assets/Scripts/ | Unity MonoBehaviour 入口 |
| GlobalConfig.asset | Assets/Resources/ | 全局配置 |
| Entry.cs | cn.codemode.helloworld/Scripts/Hotfix/Share/ | ET.Entry.Start 业务入口 |
| MainSceneViewInit.cs | cn.codemode.helloworld/Scripts/HotfixView/Client/ | 视图初始化 |
| ET.Model.asmdef | cn.codemode.demo/Runtime/Model/ | 数据层程序集定义 |
| ET.Hotfix.asmdef | cn.codemode.demo/Runtime/Hotfix/ | 逻辑层程序集定义（noEngineReferences:true） |
| ET.ModelView.asmdef | cn.codemode.demo/Runtime/ModelView/ | 表现数据层程序集定义 |
| ET.HotfixView.asmdef | cn.codemode.demo/Runtime/HotfixView/ | 表现逻辑层程序集定义 |
| CodeLoader.cs | cn.etetet.loader/Scripts/Loader/Client/ | 程序集加载器 |
| Init.cs（ET原版） | cn.etetet.loader/Scripts/Loader/Client/ | ET 原版入口（本项目未使用） |
| FiberManager.cs | cn.etetet.core/Scripts/Core/Share/World/Fiber/ | Fiber 管理 |
| GlobalConfig.cs | com.etetet.init/Runtime/ | 全局配置类定义 |

---

## 9. 扩展方向

### 9.1 接入网络通信

引入 `cn.etetet.net` 包，在 Entry.cs 中添加 NetComponent：

```csharp
scene.AddComponent<NetComponent>();
```

### 9.2 接入配置系统

引入 `cn.etetet.config` 包，在 Entry.cs 中加载配置：

```csharp
await World.Instance.AddSingleton<ConfigLoader>().LoadAsync();
```

### 9.3 完整 ET 入口（替代简化反射）

用 ET 原版的 `[Invoke(sceneType)]` 订阅 FiberInit 事件，替代 Entry.cs 中的反射：

```csharp
[Invoke(1)]  // sceneType=1 对应 GameScene
public class MainFiberInit : AInvokeHandler<FiberInit, ETTask>
{
    public override ETTask Handle(FiberInit args)
    {
        Scene scene = args.Fiber.Root;
        scene.AddComponent<PlayerComponent>();
        return ETTask.CompletedTask;
    }
}
```

### 9.4 服务端运行

将 GlobalConfig.CodeMode 改为 3（ClientServer），运行 ET 菜单 → Switch CodeMode → ClientServer，重新编译。服务端代码会编译到同一程序集，可在 Unity 中以 Play 模式启动服务端。

---

**文档版本**：v1.0
**最后更新**：2026-07-19
**适用项目**：d:\Unity\LockstepDemo\CodeMode
