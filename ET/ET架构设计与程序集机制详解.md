# ET10 架构设计与程序集机制详解

> 基于 `d:\Unity\LockstepDemo\ET` 项目完整源码分析
> 本文系统讲解ET框架的编译架构、asmdef/asmref生成机制、CodeMode切换原理，以及如何基于ET基础设施构建新项目

---

## 目录

- [一、ET架构总览](#一et架构总览)
- [二、程序集划分（asmdef）](#二程序集划分asmdef)
- [三、asmref动态生成机制（核心）](#三asmref动态生成机制核心)
- [四、CodeMode切换完整流程](#四codemode切换完整流程)
- [五、主包机制（MainPackage）](#五主包机制mainpackage)
- [六、Unity整体编译架构](#六unity整体编译架构)
- [七、服务端编译与运行](#七服务端编译与运行)
- [八、热更新机制](#八热更新机制)
- [九、基于ET基础设施构建新项目的架构规划](#九基于et基础设施构建新项目的架构规划)

---

## 一、ET架构总览

### 1.1 核心设计理念：All in One

ET **不是**三个独立的C#项目（服务端一个、共享端一个、客户端一个），而是一个 **Unity项目 + 动态CodeMode切换** 的一体化架构。

```
ET/
├── Packages/              # 所有代码都在这里（ET包 + 你的业务包）
│   ├── cn.etetet.core/             # 核心框架
│   ├── cn.etetet.statesync/        # 主包（定义asmdef）
│   ├── cn.etetet.lockstep/         # 帧同步包
│   ├── com.etetet.init/            # 初始化包（CodeMode切换逻辑）
│   └── ...
├── Assets/                # Unity资源（Plugins等）
├── Bin/                   # 编译输出目录
├── Scripts/               # PowerShell工具脚本
└── ProjectSettings/       # Unity项目设置
```

### 1.2 代码分层

每个包内部的代码按 **两个维度** 分层：

**维度1：是否热更新**
| 层级 | 说明 | 是否热更 |
|------|------|---------|
| Core | 框架核心代码 | 否 |
| Model | 数据定义（Entity/Component） | 否 |
| Hotfix | 业务逻辑（System） | 是 |
| ModelView | 表现层数据定义 | 否 |
| HotfixView | 表现层逻辑 | 是 |
| Config | 配置数据 | 否 |
| Loader | 加载器 | 否 |
| Editor | 编辑器工具 | 否 |

**维度2：运行端**
| 端 | 说明 |
|----|------|
| Share | 客户端服务端共享 |
| Client | 仅客户端 |
| Server | 仅服务端 |
| ClientServer | 仅开发模式（双端同时编译） |
| Test | 仅测试模式 |

### 1.3 包的完整目录规范

```
cn.etetet.xxx/
├── package.json                    # 包配置（依赖声明）
├── packagegit.json                 # Git配置（Id等）
├── Scripts/
│   ├── Core/                       # 不热更的核心代码
│   │   ├── Share/                  # 双端共享
│   │   ├── Client/                 # 客户端独有
│   │   └── Server/                 # 服务端独有
│   ├── Model/                      # 不热更的数据定义
│   │   ├── Share/                  # 双端共享Entity定义
│   │   ├── Client/                 # 客户端独有
│   │   └── Server/                 # 服务端独有
│   ├── Hotfix/                     # 可热更的业务逻辑
│   │   ├── Share/                  # 双端共享System
│   │   ├── Client/                 # 客户端独有
│   │   └── Server/                 # 服务端独有
│   ├── ModelView/                  # 不热更的表现层数据
│   │   └── Client/                 # 通常只有客户端
│   ├── HotfixView/                 # 可热更的表现层逻辑
│   │   └── Client/
│   ├── Loader/                     # 加载器代码
│   │   ├── Share/
│   │   ├── Client/
│   │   └── Server/
│   ├── Config/                     # 配置代码
│   └── Editor/                     # 编辑器工具
│       ├── Share/
│       └── Client/
├── CodeMode/                       # CodeMode专属代码（与Scripts同构）
│   └── Model/
│       └── Client/
├── Proto/                          # 消息定义
├── Luban/                          # 配置表
└── AGENTS.md                       # 包规范文档
```

---

## 二、程序集划分（asmdef）

### 2.1 asmdef定义在哪里

ET的asmdef文件 **只定义在"主包"的Runtime目录下**，不是每个包都有。

当前项目的主包是 `cn.etetet.statesync`，其Runtime目录下定义了所有程序集：

```
cn.etetet.statesync/Runtime/
├── Model/ET.Model.asmdef          → 定义 ET.Model 程序集
├── Hotfix/ET.Hotfix.asmdef        → 定义 ET.Hotfix 程序集
├── ModelView/ET.ModelView.asmdef  → 定义 ET.ModelView 程序集
├── HotfixView/ET.HotfixView.asmdef → 定义 ET.HotfixView 程序集
├── Config/ET.Config.asmdef        → 定义 ET.Config 程序集
└── Editor/ET.Editor.asmdef        → 定义 ET.Editor 程序集
```

另外还有两个独立的asmdef：
- `cn.etetet.core/Runtime/ET.Core.asmdef` → 定义 ET.Core 程序集
- `cn.etetet.loader/Runtime/ET.Loader.asmdef` → 定义 ET.Loader 程序集

### 2.2 各程序集的职责

| 程序集 | asmdef位置 | 职责 | 是否热更 | noEngineReferences |
|--------|-----------|------|---------|-------------------|
| ET.Core | cn.etetet.core/Runtime/ | 框架核心（Entity/EventSystem/网络等） | 否 | false |
| ET.Loader | cn.etetet.loader/Runtime/ | 代码加载器 | 否 | false |
| ET.Model | 主包/Runtime/Model/ | 数据定义（Entity/Component） | 否 | false |
| ET.Hotfix | 主包/Runtime/Hotfix/ | 业务逻辑（System） | **是** | true |
| ET.ModelView | 主包/Runtime/ModelView/ | 表现层数据 | 否 | false |
| ET.HotfixView | 主包/Runtime/HotfixView/ | 表现层逻辑 | **是** | false |
| ET.Config | 主包/Runtime/Config/ | 配置数据 | 否 | true |
| ET.Editor | 主包/Runtime/Editor/ | 编辑器工具 | 否 | false |

### 2.3 asmdef内容详解

**ET.Core.asmdef** — 核心程序集定义

参考：[ET.Core.asmdef](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Runtime/ET.Core.asmdef)

```json
{
    "name": "ET.Core",
    "rootNamespace": "ET",
    "references": ["ET.MemoryPack"],
    "allowUnsafeCode": true,
    "autoReferenced": true,
    "noEngineReferences": false
}
```

**ET.Model.asmdef** — 数据模型程序集

参考：[ET.Model.asmdef](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.statesync/Runtime/Model/ET.Model.asmdef)

```json
{
    "name": "ET.Model",
    "rootNamespace": "ET",
    "references": [
        "ET.Core", "ET.Loader", "ET.LSEntity", "ET.Recast",
        "ET.TrueSync", "ET.YooAssets", "Unity.Mathematics",
        "ET.HybridCLR", "ET.Init"
    ],
    "allowUnsafeCode": true,
    "defineConstraints": ["INITED", "IS_COMPILING || UNITY_EDITOR"],
    "noEngineReferences": false
}
```

**ET.Hotfix.asmdef** — 热更逻辑程序集（关键：noEngineReferences=true）

参考：[ET.Hotfix.asmdef](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.statesync/Runtime/Hotfix/ET.Hotfix.asmdef)

```json
{
    "name": "ET.Hotfix",
    "rootNamespace": "ET",
    "references": [
        "ET.Core", "ET.Loader", "ET.LSEntity", "ET.Model",
        "ET.Recast", "ET.TrueSync", "Unity.Mathematics",
        "ET.HybridCLR", "ET.Init", "ET.YooAssets"
    ],
    "allowUnsafeCode": true,
    "defineConstraints": ["INITED", "IS_COMPILING || UNITY_EDITOR"],
    "noEngineReferences": true   // 关键！不引用UnityEngine，可在服务端纯dotnet环境运行
}
```

> **关键设计**：`ET.Hotfix` 和 `ET.Config` 设置了 `noEngineReferences: true`，意味着这两个程序集 **不引用UnityEngine**，可以在服务端的纯.NET环境运行。这就是ET实现"双端共享逻辑代码"的基础。

### 2.4 defineConstraints的作用

```json
"defineConstraints": ["INITED", "IS_COMPILING || UNITY_EDITOR"]
```

- `INITED`：只有执行过 `Initialize-Project.ps1` 后才会定义此宏，防止未初始化时编译报错
- `IS_COMPILING || UNITY_EDITOR`：编译热更dll时定义 `IS_COMPILING`，正常Unity编辑器模式也编译

### 2.5 Ignore前缀的asmdef

部分包有一个 `Ignore.ET.XXX.asmdef` 文件，如：

参考：[Ignore.ET.Loader.asmdef](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.loader/Ignore.ET.Loader.asmdef)

```json
{
    "name": "Ignore.ET.Loader",
    "defineConstraints": ["IGNORE"]
}
```

`IGNORE` 宏永远不会被定义，所以这个程序集 **永远不会编译**。它的作用是：当CodeMode切换删除了asmref后，防止Unity把该目录的代码归入默认程序集。

---

## 三、asmref动态生成机制（核心）

### 3.1 什么是asmref

`.asmref`（Assembly Definition Reference）是Unity的文件格式，用于把一个目录的代码 **归入指定的asmdef程序集**。

```json
// AssemblyReference.asmref 文件内容
{ "reference": "ET.Model" }
```

表示：这个目录下的所有.cs文件都编译到 `ET.Model` 程序集中。

### 3.2 ET的asmref动态生成

ET **不手动维护asmref文件**，而是在CodeMode切换时由工具 **自动生成/删除**。

核心逻辑在：[CodeModeChangeHelper.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/DotNet~/CodeModeChangeHelper.cs)

### 3.3 生成算法详解

```csharp
// 遍历所有 cn.etetet.* 包
foreach (string moduleDir in Directory.GetDirectories(a, "cn.etetet.*"))
{
    string packageName = Path.GetFileName(moduleDir);
    bool isTargetPackage = targetPackages.Contains(packageName);

    // 遍历 Scripts 和 CodeMode 两种代码目录
    foreach (string scriptDir in scriptDirs)  // {"Scripts", "CodeMode"}
    {
        // 遍历所有层级 Model/Hotfix/ModelView/HotfixView/Core/Loader/Config/Editor
        foreach (string modelDir in modelDirs)
        {
            // 遍历所有端 Server/Client/Share/Test/ClientServer
            foreach (string serverDir in serverDirs)
            {
                HandleAssemblyReferenceFile(
                    codeMode, moduleDir, scriptDir,
                    modelDir, serverDir, isTargetPackage
                );
            }
        }
    }
}
```

`HandleAssemblyReferenceFile` 方法的核心逻辑：

```csharp
private static void HandleAssemblyReferenceFile(
    string codeMode, string moduleDir, string scriptDir,
    string modelDir, string serverDir, bool isTargetPackage)
{
    // asmref文件路径: 包/Scripts/Model/Client/AssemblyReference.asmref
    string filePath = Path.Combine(
        moduleDir, scriptDir, modelDir, serverDir, "AssemblyReference.asmref");

    // 第一步：先删除已有的asmref
    DeleteAssemblyReference(filePath);

    // 第二步：判断是否需要创建新的asmref
    // 条件1：是目标包（主包及其依赖），或者Editor目录（所有包的Editor都保留）
    if (isTargetPackage || modelDir == "Editor")
    {
        // 条件2：路径在白名单v集合中
        string path = $"{codeMode}/{scriptDir}/{modelDir}/{serverDir}";
        if (v.Contains(path))
        {
            // 创建asmref文件，内容为 {"reference": "ET.{modelDir}"}
            CreateAssemblyReference(filePath, modelDir);
        }
    }
}
```

生成的asmref内容：

```csharp
private static void CreateAssemblyReference(string path, string modelDir)
{
    File.WriteAllText(path, $"{{ \"reference\": \"ET.{modelDir}\" }}");
}
```

即：
- `Model` 目录的asmref → `{"reference": "ET.Model"}`
- `Hotfix` 目录的asmref → `{"reference": "ET.Hotfix"}`
- `Core` 目录的asmref → `{"reference": "ET.Core"}`
- 以此类推

### 3.4 白名单v集合

v集合定义了 **每种CodeMode下哪些路径需要生成asmref**。

参考：[CodeModeChangeHelper.cs v集合](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/DotNet~/CodeModeChangeHelper.cs#L17-L90)

**CodeMode=Client时，生成的asmref**：

| 路径模式 | asmref内容 |
|---------|-----------|
| `cn.etetet.xxx/Scripts/Model/Client/` | `{"reference":"ET.Model"}` |
| `cn.etetet.xxx/Scripts/Model/Share/` | `{"reference":"ET.Model"}` |
| `cn.etetet.xxx/Scripts/Hotfix/Client/` | `{"reference":"ET.Hotfix"}` |
| `cn.etetet.xxx/Scripts/Hotfix/Share/` | `{"reference":"ET.Hotfix"}` |
| `cn.etetet.xxx/Scripts/ModelView/Client/` | `{"reference":"ET.ModelView"}` |
| `cn.etetet.xxx/Scripts/HotfixView/Client/` | `{"reference":"ET.HotfixView"}` |
| `cn.etetet.xxx/Scripts/Core/Client/` | `{"reference":"ET.Core"}` |
| `cn.etetet.xxx/Scripts/Core/Share/` | `{"reference":"ET.Core"}` |
| `cn.etetet.xxx/Scripts/Loader/Client/` | `{"reference":"ET.Loader"}` |
| `cn.etetet.xxx/Scripts/Loader/Share/` | `{"reference":"ET.Loader"}` |

> 注意：Client模式下 **不会** 为 `Server` 目录生成asmref，所以Server代码不参与编译。

**CodeMode=Server时，生成的asmref**：

| 路径模式 | asmref内容 |
|---------|-----------|
| `cn.etetet.xxx/Scripts/Model/Server/` | `{"reference":"ET.Model"}` |
| `cn.etetet.xxx/Scripts/Model/Share/` | `{"reference":"ET.Model"}` |
| `cn.etetet.xxx/Scripts/Hotfix/Server/` | `{"reference":"ET.Hotfix"}` |
| `cn.etetet.xxx/Scripts/Hotfix/Share/` | `{"reference":"ET.Hotfix"}` |
| `cn.etetet.xxx/Scripts/Core/Server/` | `{"reference":"ET.Core"}` |
| `cn.etetet.xxx/Scripts/Core/Share/` | `{"reference":"ET.Core"}` |
| `cn.etetet.xxx/Scripts/Loader/Server/` | `{"reference":"ET.Loader"}` |
| `cn.etetet.xxx/Scripts/Loader/Share/` | `{"reference":"ET.Loader"}` |

> Server模式下 **不会** 为 `Client`、`ModelView`、`HotfixView` 生成asmref。

**CodeMode=ClientServer时（开发模式）**：同时生成Client+Server+Share的asmref，并且额外包含 `Test` 目录。

### 3.5 目标包过滤

不是所有包都会生成asmref，只有 **目标包**（主包及其依赖包）才会生成。

```csharp
bool isTargetPackage = targetPackages.Contains(packageName);
```

目标包列表来自 `MainPackage.txt` 文件（由MainPackageSelector生成）。不在列表中的包，其代码不会生成asmref，也就不会参与编译。

---

## 四、CodeMode切换完整流程

### 4.1 触发方式

**方式1：Unity菜单**

参考：[CodeModeEditor.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/Editor/CodeModeEditor.cs)

```csharp
[MenuItem("ET/Init/CodeMode")]
public static void Init()
{
    var globalConfig = Resources.Load<GlobalConfig>("GlobalConfig");
    Process process = ProcessHelper.DotNet(
        $"Bin/ET.CodeMode.dll --CodeMode={globalConfig.CodeMode}", ".", true);
    process.WaitForExit();
    AssetDatabase.Refresh();
}
```

**方式2：修改GlobalConfig**

参考：[GlobalConfigEditor.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/Editor/GlobalConfigEditor.cs)

在Inspector面板修改CodeMode字段时，自动触发切换：

```csharp
if (globalConfig.CodeMode != this.codeMode)
{
    this.codeMode = globalConfig.CodeMode;
    Process process = ProcessHelper.DotNet(
        $"Bin/ET.CodeMode.dll --CodeMode={globalConfig.CodeMode}", ".", true);
    process.WaitForExit();
    AssetDatabase.Refresh();
}
```

**方式3：编译时自动触发**

参考：[AssemblyTool.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.loader/Scripts/Editor/Share/AssemblyTool.cs)

按F6编译时也会先执行CodeMode切换：

```csharp
public static void DoCompile()
{
    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    GlobalConfig globalConfig = Resources.Load<GlobalConfig>("GlobalConfig");
    // 先切换CodeMode
    Process process = ProcessHelper.DotNet(
        $"Bin/ET.CodeMode.dll --CodeMode={globalConfig.CodeMode}", ".", true);
    process.WaitForExit();
    // 再编译dll
    bool isCompileOk = CompileDlls(globalConfig.EditorScripts);
    // 复制热更dll
    CopyHotUpdateDlls();
}
```

### 4.2 完整流程

```
用户修改CodeMode
       │
       ▼
GlobalConfigEditor 检测到变化
       │
       ▼
调用 dotnet Bin/ET.CodeMode.dll --CodeMode=Client
       │
       ▼
CodeModeChangeHelper.ChangeToCodeMode() 执行
       │
       ├── 1. 读取 MainPackage.txt 获取目标包列表
       ├── 2. 遍历所有 cn.etetet.* 包
       ├── 3. 遍历所有 Scripts/CodeMode 目录
       ├── 4. 遍历所有 Model/Hotfix/Core/... 层级
       ├── 5. 遍历所有 Client/Server/Share/... 端
       ├── 6. 删除已有的 AssemblyReference.asmref
       └── 7. 根据CodeMode和白名单v，创建新的 asmref
       │
       ▼
AssetDatabase.Refresh() 刷新Unity
       │
       ▼
Unity检测到asmref变化，重新编译程序集
```

### 4.3 CodeMode三种模式对比

| 特性 | Client | Server | ClientServer |
|------|--------|--------|-------------|
| 编译Client代码 | 是 | 否 | 是 |
| 编译Server代码 | 否 | 是 | 是 |
| 编译Share代码 | 是 | 是 | 是 |
| 编译ModelView | 是 | 否 | 是 |
| 编译HotfixView | 是 | 否 | 是 |
| 编译Test代码 | 否 | 否 | 是 |
| 用途 | 打包客户端 | 发布服务端 | 开发模式 |

---

## 五、主包机制（MainPackage）

### 5.1 什么是主包

主包是项目的 **入口包**，它定义了：
1. 所有的asmdef文件（ET.Model/ET.Hotfix等）
2. ET.sln解决方案文件
3. 项目的启动场景

当前项目有两个可选主包：
- `cn.etetet.statesync` — 状态同步MMO示例
- `cn.etetet.lockstep` — 帧同步示例

### 5.2 主包选择

参考：[MainPackageSelector.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/Editor/MainPackageSelector.cs)

在GlobalConfig的Inspector中点击"Set Main Package"按钮：

```csharp
private static void SetMainPackageBySceneName(GlobalConfig globalConfig)
{
    string sceneName = globalConfig.SceneName?.Trim();
    string packageName = $"cn.etetet.{sceneName.ToLowerInvariant()}";
    bool result = MainPackageSelector.SetAsMainPackage(packageName);
    TryLinkMainPackageSolution(packageName);
}
```

### 5.3 MainPackage.txt生成

`MainPackageSelector.SetAsMainPackage` 方法会：
1. 读取主包的 `package.json`
2. 解析dependencies中的 `cn.etetet.*` 依赖
3. 将主包名和依赖包名写入 `MainPackage.txt`

```
# MainPackage.txt 内容示例
cn.etetet.statesync
cn.etetet.achievement
cn.etetet.actorlocation
cn.etetet.aoi
...
```

这个文件被 `CodeModeChangeHelper` 读取，决定哪些包需要生成asmref。

### 5.4 ET.sln硬链接

`TryLinkMainPackageSolution` 方法会将主包中的 `ET.sln` 通过 **硬链接** 链接到项目根目录：

```csharp
// 主包的ET.sln → 项目根目录的ET.sln（硬链接）
string sourceSolutionPath = Path.Combine(projectRoot, "Packages", packageName, "ET.sln");
string rootSolutionPath = Path.Combine(projectRoot, "ET.sln");
// 创建硬链接
cmd.exe /c mklink /H "rootSolutionPath" "sourceSolutionPath"
```

这意味着 `dotnet build ET.sln` 实际编译的是主包定义的解决方案。

---

## 六、Unity整体编译架构

### 6.1 程序集依赖关系

```
┌─────────────────────────────────────────────────────────────┐
│ ET.Editor (Editor only)                                      │
│  references: ET.Model, ET.Hotfix, ET.ModelView, ET.HotfixView│
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ ET.HotfixView (热更, 引用UnityEngine)                         │
│  references: ET.Model, ET.ModelView, ET.Hotfix               │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ ET.Hotfix (热更, noEngineReferences=true, 不引用UnityEngine)   │
│  references: ET.Model, ET.Core                               │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ ET.ModelView (引用UnityEngine, Cinemachine, TMP等)            │
│  references: ET.Model, ET.Core                               │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ ET.Model (引用UnityEngine)                                    │
│  references: ET.Core, ET.Loader, ET.LSEntity, ET.Recast...   │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ ET.Core (框架核心, 引用UnityEngine)                           │
│  references: ET.MemoryPack                                    │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ ET.Loader (代码加载器)                                        │
│  references: ET.Core, ET.YooAssets, ET.HybridCLR             │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ ET.Config (配置数据, noEngineReferences=true)                 │
│  references: ET.Model, ET.Core                               │
└─────────────────────────────────────────────────────────────┘
```

### 6.2 各包代码如何归入程序集

以 `cn.etetet.lockstep` 包为例（它是statesync的依赖包，不是主包）：

```
cn.etetet.lockstep/Scripts/
├── Model/
│   ├── Share/           → asmref → ET.Model（如果CodeMode允许）
│   ├── Client/          → asmref → ET.Model（如果CodeMode=Client/ClientServer）
│   └── Server/          → asmref → ET.Model（如果CodeMode=Server/ClientServer）
├── Hotfix/
│   ├── Share/           → asmref → ET.Hotfix
│   ├── Client/          → asmref → ET.Hotfix
│   └── Server/          → asmref → ET.Hotfix
├── HotfixView/
│   └── Client/          → asmref → ET.HotfixView
└── ModelView/
    └── Client/          → asmref → ET.ModelView
```

**没有asmref的目录**（如CodeMode=Client时的Server目录）→ Unity将其归入默认程序集或忽略。

### 6.3 编译触发流程

```
1. 用户按F6（或Unity菜单 ET/Scripts/Compile）
       │
       ▼
2. AssemblyTool.DoCompile()
       │
       ├── 2a. AssetDatabase.Refresh()  刷新
       ├── 2b. 执行 CodeMode 切换（生成asmref）
       ├── 2c. CompileDlls()  编译5个dll
       │    ├── ET.Config.dll
       │    ├── ET.Model.dll
       │    ├── ET.ModelView.dll
       │    ├── ET.Hotfix.dll
       │    └── ET.HotfixView.dll
       └── 2d. CopyHotUpdateDlls()  复制到加载目录
            └── 复制到 cn.etetet.loader/Bundles/Code/
```

### 6.4 编译输出

参考：[AssemblyTool.cs DllNames](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.loader/Scripts/Editor/Share/AssemblyTool.cs#L21)

```csharp
public static readonly string[] DllNames = {
    "ET.Config", "ET.Hotfix", "ET.HotfixView", "ET.Model", "ET.ModelView"
};
```

这5个dll被编译后复制到 `Bundles/Code/` 目录，作为资源打包或热更下载。

> 注意：ET.Core 和 ET.Loader 不在热更列表中，它们是框架基础设施，不参与热更新。

---

## 七、服务端编译与运行

### 7.1 服务端编译

服务端通过 `dotnet build ET.sln` 编译，ET.sln由主包提供。

ET.sln中定义了非Unity的纯.NET项目（通过DotNet~目录）：

```
ET.sln (来自主包，硬链接到根目录)
├── ET.App         (DotNet~)  → Bin/ET.App.dll      服务端入口
├── ET.CodeMode    (DotNet~)  → Bin/ET.CodeMode.dll  CodeMode工具
├── ET.Loader      (DotNet~)  → Bin/ET.Loader.dll    加载器
├── ET.Core        (DotNet~)  → 编译核心
├── ET.Model       (DotNet~)  → 编译数据模型
├── ET.Hotfix      (DotNet~)  → 编译热更逻辑
└── ...
```

每个包的 `DotNet~` 目录包含纯.NET的csproj，用于服务端编译。

### 7.2 服务端运行

```powershell
# 开发模式：Unity内运行
# Unity菜单 → ET → Loader → Server Tools → Start Server(Single Process)

# 独立进程运行
dotnet.exe Bin/ET.App.dll --Console=1 --CodeMode=Server

# 运行目录必须是项目根目录（Bin的上一层），不是Bin目录
```

### 7.3 服务端热重载

服务端运行时可以热重载Hotfix dll：

```powershell
# 1. 修改代码后编译
dotnet build ET.sln

# 2. 服务端控制台输入命令，或通过管理接口触发重载
# 服务端会重新加载 ET.Hotfix.dll
```

---

## 八、热更新机制

### 8.1 客户端热更新

ET客户端热更新基于 **HybridCLR**（原huatuo）：

```
不热更的程序集（打包到客户端）：
├── ET.Core.dll          框架核心
├── ET.Loader.dll        加载器
├── ET.Model.dll         数据模型
├── ET.ModelView.dll     表现层数据
└── ET.Config.dll        配置

可热更的程序集（通过YooAsset下载更新）：
├── ET.Hotfix.dll        业务逻辑
└── ET.HotfixView.dll    表现层逻辑
```

### 8.2 热更新流程

```
1. 客户端启动
       │
       ▼
2. ET.Loader 加载 ET.Core/ET.Model/ET.ModelView（内置dll）
       │
       ▼
3. YooAsset 检查热更资源，下载新的 ET.Hotfix.dll/ET.HotfixView.dll
       │
       ▼
4. HybridCLR 加载热更dll
       │
       ▼
5. 运行时按F7可重新加载Hotfix dll（开发模式热重载）
```

### 8.3 关键设计：noEngineReferences

`ET.Hotfix.asmdef` 设置 `noEngineReferences: true`，这意味着：
- Hotfix代码 **不直接引用UnityEngine**
- 需要Unity功能时，通过Model/ModelView层的Component间接调用
- 这样Hotfix.dll可以在服务端纯.NET环境运行，也可以在客户端HybridCLR环境运行

---

## 九、基于ET基础设施构建新项目的架构规划

### 9.1 可直接使用的ET基础设施包

| 包名 | 依赖 | 能力 |
|------|------|------|
| cn.etetet.sourcegenerator | 无ET依赖 | 代码生成器+分析器 |
| cn.etetet.memorypack | 无ET依赖 | MemoryPack序列化 |
| cn.etetet.mathematics | 无ET依赖 | 数学库 |
| cn.etetet.truesync | 无ET依赖 | 定点数（帧同步） |
| cn.etetet.core | sourcegenerator, memorypack | Entity/Component/EventSystem/Network/Timer/ETTask/Fiber |
| com.etetet.init | 无ET依赖 | CodeMode机制/GlobalConfig |

### 9.2 新项目架构规划

```
MyGame/
├── Packages/
│   ├── manifest.json              # 引用ET包
│   │
│   ├── com.etetet.init/           # 直接引用ET包（不修改）
│   ├── cn.etetet.core/            # 直接引用ET包
│   ├── cn.etetet.sourcegenerator/ # 直接引用ET包
│   ├── cn.etetet.memorypack/      # 直接引用ET包
│   ├── cn.etetet.loader/          # 直接引用ET包
│   ├── cn.etetet.config/          # 按需引用
│   ├── cn.etetet.proto/           # 按需引用
│   ├── cn.etetet.unit/            # 按需引用
│   │
│   ├── com.mygame.main/           # 你的主包（定义asmdef和ET.sln）
│   │   ├── Runtime/
│   │   │   ├── Model/ET.Model.asmdef
│   │   │   ├── Hotfix/ET.Hotfix.asmdef
│   │   │   ├── ModelView/ET.ModelView.asmdef
│   │   │   ├── HotfixView/ET.HotfixView.asmdef
│   │   │   └── Config/ET.Config.asmdef
│   │   ├── Scenes/
│   │   │   └── Init.unity
│   │   ├── ET.sln
│   │   └── package.json
│   │
│   ├── cn.mygame.login/           # 你的业务包
│   │   ├── Scripts/
│   │   │   ├── Model/Share/
│   │   │   │   └── Account.cs
│   │   │   ├── Hotfix/Share/
│   │   │   │   └── AccountSystem.cs
│   │   │   └── ...
│   │   ├── Proto/
│   │   └── package.json
│   │
│   └── cn.mygame.battle/          # 你的业务包
│       └── ...
│
├── Assets/
│   └── Plugins/
├── Bin/                           # 编译输出
├── Scripts/                       # 工具脚本
└── ProjectSettings/
```

### 9.3 manifest.json配置

```json
{
    "dependencies": {
        "com.etetet.init": "file:../../ET/Packages/com.etetet.init",
        "cn.etetet.core": "file:../../ET/Packages/cn.etetet.core",
        "cn.etetet.sourcegenerator": "file:../../ET/Packages/cn.etetet.sourcegenerator",
        "cn.etetet.memorypack": "file:../../ET/Packages/cn.etetet.memorypack",
        "cn.etetet.loader": "file:../../ET/Packages/cn.etetet.loader",
        "cn.etetet.config": "file:../../ET/Packages/cn.etetet.config",
        "cn.etetet.proto": "file:../../ET/Packages/cn.etetet.proto",
        "com.mygame.main": "file:Packages/com.mygame.main",
        "cn.mygame.login": "file:Packages/cn.mygame.login",
        "cn.mygame.battle": "file:Packages/cn.mygame.battle"
    }
}
```

### 9.4 主包的asmdef定义

在 `com.mygame.main/Runtime/` 下创建asmdef（参考statesync主包）：

**ET.Model.asmdef**:
```json
{
    "name": "ET.Model",
    "rootNamespace": "ET",
    "references": ["ET.Core", "ET.Loader", "ET.Init"],
    "allowUnsafeCode": true,
    "defineConstraints": ["INITED", "IS_COMPILING || UNITY_EDITOR"]
}
```

**ET.Hotfix.asmdef**:
```json
{
    "name": "ET.Hotfix",
    "rootNamespace": "ET",
    "references": ["ET.Core", "ET.Model"],
    "allowUnsafeCode": true,
    "defineConstraints": ["INITED", "IS_COMPILING || UNITY_EDITOR"],
    "noEngineReferences": true
}
```

### 9.5 业务包的package.json

```json
{
    "name": "cn.mygame.login",
    "version": "1.0.0",
    "dependencies": {
        "cn.etetet.core": "1.0.0",
        "cn.etetet.proto": "1.0.0"
    }
}
```

### 9.6 关键原则

1. **不修改ET包代码**：直接引用，保持可更新
2. **包依赖单向**：你的业务包依赖ET包，ET包永远不依赖你的包
3. **唯一编译入口**：始终用 `dotnet build ET.sln`
4. **CodeMode切换**：复用ET的CodeMode机制
5. **PackageType唯一**：每个自定义包的PackageType.cs中的Id必须全局唯一
6. **热更边界**：Model层放数据（不热更），Hotfix层放逻辑（热更）
7. **MainPackage.txt**：设置主包后，只有主包及其依赖包参与编译

### 9.7 搭建步骤

```powershell
# 1. 创建Unity 2022.3.62项目
# 2. 引入ET包（git submodule或file引用）
git submodule add https://github.com/egametang/ET.git ET

# 3. 配置manifest.json引用ET包

# 4. 创建主包 com.mygame.main
#    - 复制statesync主包的Runtime目录结构（asmdef文件）
#    - 创建ET.sln
#    - 创建Init场景

# 5. 创建业务包 cn.mygame.*

# 6. 初始化
pwsh ./Scripts/Initialize-Project.ps1

# 7. 设置主包
# Unity → GlobalConfig → SceneName设为"mygame.main" → Set Main Package

# 8. 切换CodeMode
# Unity → ET → Init → CodeMode

# 9. 编译
dotnet build ET.sln
# 或 Unity中按F6
```

---

## 附录：关键文件索引

| 文件 | 说明 |
|------|------|
| [GlobalConfig.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/Runtime/GlobalConfig.cs) | CodeMode枚举定义 |
| [CodeModeChangeHelper.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/DotNet~/CodeModeChangeHelper.cs) | asmref动态生成核心逻辑 |
| [CodeModeEditor.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/Editor/CodeModeEditor.cs) | Unity菜单触发CodeMode切换 |
| [GlobalConfigEditor.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/Editor/GlobalConfigEditor.cs) | Inspector面板CodeMode切换+主包设置 |
| [MainPackageSelector.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/Editor/MainPackageSelector.cs) | 主包选择和MainPackage.txt生成 |
| [DependencyResolver.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/Editor/DependencyResolver.cs) | 包依赖解析器 |
| [AssemblyTool.cs](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.loader/Scripts/Editor/Share/AssemblyTool.cs) | 编译工具（F6编译/F7热重载） |
| [ET.Core.asmdef](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/Runtime/ET.Core.asmdef) | 核心程序集定义 |
| [ET.Model.asmdef](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.statesync/Runtime/Model/ET.Model.asmdef) | 数据模型程序集定义 |
| [ET.Hotfix.asmdef](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.statesync/Runtime/Hotfix/ET.Hotfix.asmdef) | 热更逻辑程序集定义 |
| [ET.Loader.asmdef](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.loader/Runtime/ET.Loader.asmdef) | 加载器程序集定义 |
| [AGENTS.md](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.harness/AGENTS.md) | 项目规范和包层级定义 |
