# ET All-in-One + CodeMode 架构新手示例

> 一个最小化、可独立运行、不依赖完整 ET 包的示例，用于直观理解 ET 的 **All-in-One 架构** 与 **CodeMode 切换机制**。

---

## 目录

- [第1章 什么是 All-in-One 架构](#第1章-什么是-all-in-one-架构)
- [第2章 什么是 CodeMode](#第2章-什么是-codemode)
- [第3章 asmdef 与 asmref 原理](#第3章-asmdef-与-asmref-原理)
- [第4章 目录约定](#第4章-目录约定)
- [第5章 切换流程](#第5章-切换流程)
- [第6章 三种模式产物对比表](#第6章-三种模式产物对比表)
- [第7章 与 ET 原版的对应关系](#第7章-与-et-原版的对应关系)
- [第8章 常见疑问 FAQ](#第8章-常见疑问-faq)
- [第9章 动手练习](#第9章-动手练习)

---

## 本示例目的

ET 框架的"All-in-One + CodeMode"机制是新手最容易困惑的部分之一：

- 一个 Unity 工程怎么既出客户端、又出服务端？
- 同一份代码，为什么切换模式后有些就不参与编译了？
- `asmdef` 和 `asmref` 到底是什么关系？
- `Model / Hotfix / ModelView / HotfixView` 这一堆程序集怎么区分？

本示例通过**最小化目录结构 + PowerShell 切换脚本 + 验证脚本 + C# 工具**，让新手能亲手切换三种 CodeMode，看到 `AssemblyReference.asmref` 文件的生成与删除过程，直观理解 ET 的核心编译机制。

## 目标读者

- 第一次接触 ET 框架的 Unity 开发者
- 想了解 ET 多程序集机制的服务端开发者
- 对 `asmdef / asmref` 不熟悉的 Unity 新手
- 希望复刻 ET 架构做自研框架的进阶开发者

> 阅读本文不需要你写过 ET 代码，但需要对 Unity 和 C# 有基础认知。

---

## 第1章 什么是 All-in-One 架构

### 1.1 传统做法：三个独立项目

在传统的 Unity 联网游戏开发中，通常需要拆分成 3 个独立的项目：

```
传统 3 项目结构
├── MyGame-Server/         ← 服务端项目（.NET 控制台/ASP.NET）
│   └── Server.sln
├── MyGame-Client/         ← 客户端项目（Unity 工程）
│   └── Client.sln
└── MyGame-Share/          ← 共享库（双方都引用）
    └── Share.dll
```

这种做法的问题非常明显：

1. **代码重复**：很多数据类型（玩家、战斗、消息）双方都需要，但分项目后只能复制粘贴或者用共享库引用。
2. **共享库依赖混乱**：共享库如果引用了 `UnityEngine`，服务端就跑不起来；如果不引用，客户端表现层又用不了。
3. **热更独立管理**：客户端热更和服务端热更要分两套机制，维护成本翻倍。
4. **联调困难**：本地同时调试 Client 和 Server，需要在两个 IDE 之间切换、两个项目同时启动。
5. **配置同步麻烦**：协议、数值表等双方都要用，容易不同步。

### 1.2 ET 的做法：一个 Unity 项目 + CodeMode 切换

ET 选择"All-in-One"：**一个 Unity 工程同时承担客户端、服务端、共享库三种角色**。通过 `CodeMode` 这个变量决定当前编译什么：

```
ET All-in-One 项目（本示例）
└── CodeMode/
    ├── Packages/
    │   ├── cn.codemode.demo/             ← 主包：定义 6 个 asmdef
    │   │   └── Runtime/
    │   │       ├── Model/ET.Model.asmdef
    │   │       ├── Hotfix/ET.Hotfix.asmdef
    │   │       ├── ModelView/ET.ModelView.asmdef
    │   │       ├── HotfixView/ET.HotfixView.asmdef
    │   │       ├── Config/ET.Config.asmdef
    │   │       └── Editor/ET.Editor.asmdef
    │   └── cn.codemode.helloworld/       ← 业务包：Share/Client/Server 三套代码
    │       └── Scripts/
    │           ├── Model/{Share,Client,Server}
    │           ├── Hotfix/{Share,Client,Server}
    │           └── HotfixView/{Client,Server}
    └── Tools/
        ├── Switch-CodeMode.ps1           ← 切换脚本
        ├── Verify-CodeMode.ps1           ← 验证脚本
        └── CodeModeChangeHelper/         ← C# 工具（与 ET 原版对应）
```

切换 CodeMode 时，脚本会增删 `AssemblyReference.asmref` 文件，决定哪些目录的代码参与编译——这就是 All-in-One 的核心。

### 1.3 对比表：3 项目 vs All-in-One

| 维度 | 3 个独立项目 | ET All-in-One |
| --- | --- | --- |
| 项目数量 | 3 个 sln | 1 个 Unity 工程 |
| 共享代码方式 | 引用 Share.dll | 同目录 Share/ 子文件夹 |
| 客户端/服务端代码隔离 | 物理隔离（不同仓库） | 逻辑隔离（不同子目录 + asmref 增删） |
| 协议同步 | 需要工具同步 | 同源，无需同步 |
| 联调成本 | 启动两个 IDE | 一个 Unity 编辑器内同时跑 C/S |
| 热更管理 | 两套热更机制 | 统一通过 Hotfix 层热更 |
| 学习成本 | 低（直观） | 高（需理解 asmdef/asmref） |
| 服务端纯 .NET 运行 | 容易 | 需 `noEngineReferences:true` + 不引用 UnityEngine |
| 适合项目 | 小型、客户端/服务端语言不同的项目 | C# 全栈、客户端服务端同源的项目 |

### 1.4 为什么 ET 选 All-in-One

ET 是 **C# 全栈**框架，客户端和服务端都用 C#，天然适合 All-in-One：

1. **避免代码重复**：战斗逻辑、AI、数值等可放在 Share 层，C/S 共用一份源码。
2. **共享类型定义**：消息、组件、Entity 类型只需要定义一次，编译时根据 CodeMode 自动分到客户端 dll 或服务端 dll。
3. **热更统一管理**：Hotfix 层是热更代码，C/S 都通过同一套机制热更，无需两套基础设施。
4. **本地联调极快**：`ClientServer` 模式下，Unity 编辑器内同时运行客户端逻辑和服务端逻辑，断点调试一条龙。
5. **协议零同步**：消息体直接定义在 Share 层，序列化用同一套 BSON/Protobuf，没有"客户端协议与服务端不一致"的问题。

> 简单说：**ET 用 Unity 工程作为"代码仓库"，用 CodeMode 作为"编译开关"，把传统 3 项目的痛点一次性解决。**

---

## 第2章 什么是 CodeMode

### 2.1 三种 CodeMode 定义

`CodeMode` 是一个枚举值，决定当前 Unity 工程编译出哪一组 dll。本示例支持三种：

| CodeMode | 编译范围 | 产出 dll | 典型用途 |
| --- | --- | --- | --- |
| **Client** | Share + Client 代码 | 客户端 dll（含 UnityEngine） | 发布客户端包 |
| **Server** | Share + Server 代码 | 服务端 dll（不含 UnityEngine） | 发布服务端包、纯 .NET 运行 |
| **ClientServer** | Share + Client + Server 全部代码 | 联调 dll（含 UnityEngine + Server 代码） | Unity 编辑器内本地联调 |

### 2.2 三种模式的使用场景

```
开发期（本地调试）
        │
        ▼
   CodeMode = ClientServer
   （一个 Unity 编辑器同时跑 C/S，断点调试）
        │
        │ 发布阶段
        ▼
   ┌────────┴────────┐
   ▼                 ▼
CodeMode = Client  CodeMode = Server
（出客户端包）      （出服务端 dll）
```

- **开发期**：始终用 `ClientServer`，方便在 Unity 编辑器内同时调试客户端表现层和服务端权威逻辑。
- **客户端发布期**：切到 `Client`，剔除所有 Server 代码，减小包体、避免泄露服务端逻辑。
- **服务端发布期**：切到 `Server`，剔除所有 Client 代码，纯 .NET 进程运行，不需要 Unity 引擎。

### 2.3 切换示意图（文字版流程图）

```
                ┌──────────────────────────┐
                │  Switch-CodeMode.ps1     │
                │  -CodeMode <mode>        │
                └────────────┬─────────────┘
                             │
              ┌──────────────┴──────────────┐
              ▼                              ▼
   删除所有 Scripts/**/                按 mode 规则在指定
   AssemblyReference.asmref           目录生成新的 asmref
              │                              │
              └──────────────┬───────────────┘
                             ▼
              Unity 检测到文件变化，触发重编译
                             │
                             ▼
              只编译有 asmref 的目录，归属到 asmdef
                             │
                             ▼
              产物：一组 dll（含/不含 C/S 代码）
```

---

## 第3章 asmdef 与 asmref 原理

### 3.1 asmdef（Assembly Definition）

`asmdef`（Assembly Definition）是 Unity 的程序集定义文件，**定义一个程序集**。一个 asmdef 文件 = 一个 .NET dll。

关键字段说明：

| 字段 | 含义 |
| --- | --- |
| `name` | 程序集名称，例如 `ET.Hotfix` |
| `rootNamespace` | 默认命名空间 |
| `references` | 引用的其他 asmdef 程序集 |
| `includePlatforms` / `excludePlatforms` | 在哪些平台编译 |
| `noEngineReferences` | **是否不引用 UnityEngine**（true = 纯 .NET，可被服务端使用） |
| `autoReferenced` | 是否被默认 asmdef 引用 |
| `allowUnsafeCode` | 是否允许 unsafe 代码 |

本示例的 `ET.Hotfix.asmdef` 完整内容（`Packages/cn.codemode.demo/Runtime/Hotfix/ET.Hotfix.asmdef`）：

```json
{
    "name": "ET.Hotfix",
    "rootNamespace": "ET",
    "references": ["ET.Model"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": true,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

关键点：
- `noEngineReferences: true` —— Hotfix 层不依赖 UnityEngine，方便服务端纯 .NET 运行、方便 HybridCLR 加载。
- `references: ["ET.Model"]` —— Hotfix 引用 Model 层，逻辑层访问数据层。

本示例共定义 6 个 asmdef：

| asmdef 文件 | name | noEngineReferences | references | 说明 |
| --- | --- | --- | --- | --- |
| `Runtime/Model/ET.Model.asmdef` | `ET.Model` | false | [] | 数据模型（可引用 UnityEngine） |
| `Runtime/Hotfix/ET.Hotfix.asmdef` | `ET.Hotfix` | **true** | [ET.Model] | 业务逻辑（不引用 UnityEngine） |
| `Runtime/ModelView/ET.ModelView.asmdef` | `ET.ModelView` | false | [ET.Model] | 表现层数据（引用 UnityEngine） |
| `Runtime/HotfixView/ET.HotfixView.asmdef` | `ET.HotfixView` | **true** | [ET.ModelView, ET.Hotfix] | 表现层逻辑（不引用 UnityEngine） |
| `Runtime/Config/ET.Config.asmdef` | `ET.Config` | **true** | [] | 配置层（不引用 UnityEngine） |
| `Runtime/Editor/ET.Editor.asmdef` | `ET.Editor` | false | [ET.Model] | 编辑器扩展（仅 Editor 平台） |

### 3.2 asmref（Assembly Definition Reference）

`asmref`（Assembly Definition Reference）是程序集引用文件，**表示"本目录及子目录下的代码归属到指定的 asmdef 程序集"**。

asmref 文件内容极简，只有一个 `reference` 字段指向某个 asmdef 的 name：

```json
{ "reference": "ET.Hotfix" }
```

这一行的意思是：**"我这个目录下的所有 .cs 文件，请编译到 `ET.Hotfix` 这个程序集里。"**

### 3.3 asmdef 与 asmref 的协作图

```
主包 cn.codemode.demo                            业务包 cn.codemode.helloworld
┌──────────────────────────────┐                ┌──────────────────────────────────┐
│ Runtime/                     │                │ Scripts/                         │
│   Hotfix/                    │                │   Hotfix/                        │
│     ET.Hotfix.asmdef  ◄──────┼────────────────┼── Client/AssemblyReference.asmref│
│         (定义程序集)          │                │   Hotfix/                        │
│                              │                │   ├── Share/AssemblyReference.asmref
│                              │                │   └── Server/AssemblyReference.asmref
└──────────────────────────────┘                └──────────────────────────────────┘

  asmdef = 程序集的"出生证"                        asmref = 业务代码的"户口本"
  （定义这个程序集叫什么）                          （声明这块代码归哪个程序集）
```

### 3.4 关键点：asmref 的存在/缺失 = 代码是否被编译

这是整个 CodeMode 机制的**核心规则**：

- **目录下有 asmref 文件** → 该目录及子目录的 .cs 被编译进 asmref 指向的 asmdef 程序集。
- **目录下没有 asmref 文件** → 该目录及子目录的 .cs **完全不参与编译**（Unity 会忽略它们）。

切换 CodeMode 的本质，就是**在业务包的各个子目录下增删 `AssemblyReference.asmref` 文件**，控制哪些代码参与当前编译。

> 这种"删文件即剔除编译"的机制非常巧妙：源码永远在硬盘上不删，但通过增删一个 1KB 的 asmref 文件，就能精确控制每个目录的编译归属。

---

## 第4章 目录约定

### 4.1 6 个 ModelDir（程序集维度）

ET 把代码按职责拆成 6 个程序集目录（ModelDir）：

| ModelDir | 类型 | 可热更 | 引用 UnityEngine | 职责 |
| --- | --- | --- | --- | --- |
| `Model` | 数据模型 | 否 | 是（可引用） | Entity / Component / 数据结构定义 |
| `Hotfix` | 业务逻辑 | **是** | 否（`noEngineReferences:true`） | System / 业务方法，热更入口 |
| `ModelView` | 表现层数据 | 否 | 是 | 客户端表现层组件（UI / 动画 / 特效数据） |
| `HotfixView` | 表现层逻辑 | **是** | 否（`noEngineReferences:true`） | 表现层 System，热更表现层逻辑 |
| `Config` | 配置 | 否 | 否（`noEngineReferences:true`） | Luban / 数值表配置类 |
| `Editor` | 编辑器扩展 | 否 | 是 | 仅 Editor 平台编译，自定义 Inspector / 工具 |

> **为什么 Hotfix / HotfixView / Config 都设 `noEngineReferences:true`？**
> 因为这三层都需要在**服务端纯 .NET 进程**中运行，或被 **HybridCLR** 加载。它们不能直接 `using UnityEngine;`。

### 4.2 3 种归属 ServerDir（端维度）

每个 ModelDir 下再按"端归属"拆分：

| ServerDir | 含义 | 是否引用 UnityEngine | 在哪些 CodeMode 下编译 |
| --- | --- | --- | --- |
| `Share` | 客户端 + 服务端共享 | 否 | Client / Server / ClientServer **全部** |
| `Client` | 仅客户端 | 通常引用 | Client / ClientServer |
| `Server` | 仅服务端 | 否 | Server / ClientServer |

### 4.3 完整目录结构示例

本示例业务包 `cn.codemode.helloworld` 的实际目录：

```
Packages/cn.codemode.helloworld/
└── Scripts/
    ├── Model/                          ← 数据模型层（不可热更）
    │   ├── Share/                      ← 共享数据（不引用 UnityEngine）
    │   │   └── HelloWorldBase.cs
    │   ├── Client/                     ← 客户端数据（引用 UnityEngine）
    │   │   └── ClientData.cs
    │   └── Server/                     ← 服务端数据（不引用 UnityEngine）
    │       └── ServerData.cs
    ├── Hotfix/                         ← 业务逻辑层（可热更，noEngineReferences=true）
    │   ├── Share/                      ← 共享逻辑
    │   │   └── HelloWorldSystem.cs
    │   ├── Client/                     ← 客户端逻辑（含 Debug.Log）
    │   │   └── ClientHelloWorldSystem.cs
    │   └── Server/                     ← 服务端逻辑（含 Console.WriteLine）
    │       └── ServerHelloWorldSystem.cs
    ├── HotfixView/                     ← 表现层逻辑（可热更）
    │   ├── Client/                     ← 客户端表现层（动画/特效/UI）
    │   │   └── ClientViewSystem.cs
    │   └── Server/                     ← 服务端"表现层"（实际用于场景对象管理）
    │       └── ServerViewSystem.cs
    ├── ModelView/                      ← 表现层数据（本示例未放代码，结构同上）
    └── Config/                         ← 配置层（本示例未放代码，结构同上）
```

> 注意：每个 `Share / Client / Server` 子目录下都可能会有一个 `AssemblyReference.asmref` 文件（切换 CodeMode 时动态增删）。本示例目录树中没有展示这些 asmref，因为它们的状态取决于当前 CodeMode。

### 4.4 各 ModelDir 职责详解

#### Model（数据模型，不可热更）

放 Entity、Component、数据结构定义。本示例 `HelloWorldBase.cs`：

```csharp
// Scripts/Model/Share/HelloWorldBase.cs
// 共享数据模型：可被 Client 和 Server 同时引用
// 不引用 UnityEngine，保证服务端可纯 .NET 运行
namespace ET
{
    public class HelloWorldBase
    {
        public long Id;
        public string Name;

        public HelloWorldBase(long id, string name)
        {
            this.Id = id;
            this.Name = name;
        }
    }
}
```

#### Hotfix（业务逻辑，可热更）

放 System、业务方法。`noEngineReferences:true`，不能直接 `using UnityEngine`。本示例 `HelloWorldSystem.cs`：

```csharp
// Scripts/Hotfix/Share/HelloWorldSystem.cs
// 共享逻辑：可被 Client 和 Server 同时调用
using System;

namespace ET
{
    public static class HelloWorldSystem
    {
        public static string SayHello(HelloWorldBase data)
        {
            return $"Hello, I'm {data.Name} (Id={data.Id}). Time={DateTime.Now:HH:mm:ss}";
        }
    }
}
```

#### ModelView（表现层数据，不可热更）

放客户端表现层组件（UI 引用、Animator 引用、特效资源等）。引用 UnityEngine。

#### HotfixView（表现层逻辑，可热更）

放表现层 System（动画切换、UI 刷新、特效触发）。`noEngineReferences:true`。本示例 `ClientViewSystem.cs`：

```csharp
// Scripts/HotfixView/Client/ClientViewSystem.cs
using UnityEngine;

namespace ET.Client
{
    public static class ClientViewSystem
    {
        public static void PlayEffect(Vector3 pos)
        {
            Debug.Log($"[ClientView] 播放特效于 {pos}");
        }

        public static void UpdateAnimation(string stateName)
        {
            Debug.Log($"[ClientView] 切换动画到 {stateName}");
        }
    }
}
```

#### Config（配置层，不引用 UnityEngine）

放 Luban / Proto 生成的配置类。`noEngineReferences:true`，C/S 共享。

#### Editor（编辑器扩展）

仅 Editor 平台编译，放自定义 Inspector、菜单工具等。

---

## 第5章 切换流程

### 5.1 完整流程（6 步）

```
1. 用户执行 Switch-CodeMode.ps1 -CodeMode <mode>
       │
       ▼
2. 脚本扫描业务包所有 Scripts/**/ 目录
       │
       ▼
3. 脚本删除所有已存在的 AssemblyReference.asmref 文件
       │
       ▼
4. 脚本根据 CodeMode 规则，在指定目录生成新的 AssemblyReference.asmref
   （文件内容为 { "reference": "ET.<ModelDir>" }）
       │
       ▼
5. Unity 检测到文件系统变化，自动触发重新编译
       │
       ▼
6. Unity 只编译存在 asmref 的目录代码，归属到对应 asmdef
       │
       ▼
   最终产物：一组 dll（含/不含 Client/Server 代码）
```

### 5.2 切换规则表

下表展示每种 CodeMode 下，业务包各子目录是否应该有 asmref 文件：

| ModelDir \ ServerDir | Share | Client | Server |
| --- | --- | --- | --- |
| **Client 模式** | Model / Hotfix / ModelView / HotfixView / Config / Editor 全有 asmref | 全有 asmref | **全无 asmref** |
| **Server 模式** | 全有 asmref | **全无 asmref** | 全有 asmref |
| **ClientServer 模式** | 全有 asmref | 全有 asmref | 全有 asmref |

> 简化记忆：**Share 永远有，Client 模式只留 Client，Server 模式只留 Server，ClientServer 模式全留。**

### 5.3 切换前 → 切换中 → 切换后的目录状态

以 **切换到 Client 模式** 为例：

**切换前**（假设当前是 ClientServer 模式）：

```
Scripts/Model/
├── Share/AssemblyReference.asmref     ← 存在
├── Client/AssemblyReference.asmref    ← 存在
└── Server/AssemblyReference.asmref    ← 存在
```

**切换中**（脚本先删除所有 asmref）：

```
Scripts/Model/
├── Share/                             ← asmref 已删
├── Client/                            ← asmref 已删
└── Server/                            ← asmref 已删
```

**切换后**（脚本按 Client 规则生成新 asmref）：

```
Scripts/Model/
├── Share/AssemblyReference.asmref     ← 重新生成（reference: ET.Model）
├── Client/AssemblyReference.asmref    ← 重新生成（reference: ET.Model）
└── Server/                            ← 没有生成 asmref，此目录代码不参与编译
```

### 5.4 实际命令示例

```powershell
cd d:\Unity\LockstepDemo\CodeMode\Tools

# 切换到 Client 模式（发布客户端时用）
.\Switch-CodeMode.ps1 -CodeMode Client

# 切换到 Server 模式（发布服务端时用）
.\Switch-CodeMode.ps1 -CodeMode Server

# 切换到 ClientServer 模式（本地联调时用）
.\Switch-CodeMode.ps1 -CodeMode ClientServer
```

脚本执行后会输出每一步的删除/创建日志，例如：

```
[Switch-CodeMode] CodeMode = Client
[Delete] Scripts/Model/Server/AssemblyReference.asmref
[Delete] Scripts/Hotfix/Server/AssemblyReference.asmref
[Delete] Scripts/HotfixView/Server/AssemblyReference.asmref
[Create] Scripts/Model/Share/AssemblyReference.asmref -> ET.Model
[Create] Scripts/Model/Client/AssemblyReference.asmref -> ET.Model
[Create] Scripts/Hotfix/Share/AssemblyReference.asmref -> ET.Hotfix
[Create] Scripts/Hotfix/Client/AssemblyReference.asmref -> ET.Hotfix
[Create] Scripts/HotfixView/Client/AssemblyReference.asmref -> ET.HotfixView
...
[Done] CodeMode 切换完成，请等待 Unity 重新编译。
```

也可以使用 C# 工具（与 ET 原版 `CodeModeChangeHelper.cs` 对应）：

```powershell
cd d:\Unity\LockstepDemo\CodeMode
dotnet run --project Tools/CodeModeChangeHelper -- --CodeMode=Client
```

---

## 第6章 三种模式产物对比表

### 6.1 asmref 存在性对比表

| 目录 | Client 模式 | Server 模式 | ClientServer 模式 |
| --- | --- | --- | --- |
| `Scripts/Model/Share/` | ✅ 有 asmref | ✅ 有 asmref | ✅ 有 asmref |
| `Scripts/Model/Client/` | ✅ 有 asmref | ❌ 无 asmref | ✅ 有 asmref |
| `Scripts/Model/Server/` | ❌ 无 asmref | ✅ 有 asmref | ✅ 有 asmref |
| `Scripts/Hotfix/Share/` | ✅ 有 asmref | ✅ 有 asmref | ✅ 有 asmref |
| `Scripts/Hotfix/Client/` | ✅ 有 asmref | ❌ 无 asmref | ✅ 有 asmref |
| `Scripts/Hotfix/Server/` | ❌ 无 asmref | ✅ 有 asmref | ✅ 有 asmref |
| `Scripts/HotfixView/Share/` | ✅ 有 asmref | ✅ 有 asmref | ✅ 有 asmref |
| `Scripts/HotfixView/Client/` | ✅ 有 asmref | ❌ 无 asmref | ✅ 有 asmref |
| `Scripts/HotfixView/Server/` | ❌ 无 asmref | ✅ 有 asmref | ✅ 有 asmref |
| `Scripts/ModelView/*/` | 同上规则 | 同上规则 | 同上规则 |
| `Scripts/Config/Share/` | ✅ 有 asmref | ✅ 有 asmref | ✅ 有 asmref |

### 6.2 编译产物对比表

| 维度 | Client 模式 | Server 模式 | ClientServer 模式 |
| --- | --- | --- | --- |
| **Share 代码** | 编译 | 编译 | 编译 |
| **Client 代码** | 编译 | 不编译 | 编译 |
| **Server 代码** | 不编译 | 编译 | 编译 |
| **`ET.Model.dll` 包含** | Share + Client 数据 | Share + Server 数据 | Share + Client + Server 数据 |
| **`ET.Hotfix.dll` 包含** | Share + Client 逻辑 | Share + Server 逻辑 | Share + Client + Server 逻辑 |
| **是否含 UnityEngine** | 是 | 否（纯 .NET） | 是 |
| **可运行环境** | Unity 客户端 | .NET 服务端进程 | Unity 编辑器（联调） |
| **典型用途** | 打包客户端 App | 部署服务端 | 开发期本地调试 |

### 6.3 每种模式的实际用途

#### Client 模式

- **何时用**：发布客户端包（出 APK / 出 exe / 出 iOS 包）。
- **特点**：剔除所有 `Server/` 目录代码，包体更小、服务端逻辑不外泄。
- **产物**：`ET.Model.dll`、`ET.Hotfix.dll`、`ET.ModelView.dll`、`ET.HotfixView.dll` 等含 UnityEngine 的客户端 dll。

#### Server 模式

- **何时用**：发布服务端、部署到 Linux/Windows 服务器。
- **特点**：剔除所有 `Client/` 目录代码，不引用 UnityEngine，可在纯 .NET 进程运行（不需要 Unity 引擎）。
- **产物**：`ET.Model.dll`、`ET.Hotfix.dll` 等纯 .NET dll，可用 `dotnet run` 直接启动。

#### ClientServer 模式

- **何时用**：开发期本地联调。
- **特点**：Share + Client + Server 全部代码参与编译，Unity 编辑器内同时跑客户端和服务端逻辑。
- **产物**：一组联调 dll，客户端逻辑和服务端逻辑都在 Unity 进程内执行，方便断点调试。
- **注意**：此模式产物体积最大，且 Server 代码也会被编译进含 UnityEngine 的 dll，**不能用于发布服务端**。

---

## 第7章 与 ET 原版的对应关系

本示例是为了让新手理解 CodeMode 机制而做的**极简版本**。ET 原版（[ET 框架](https://github.com/egametang/ET)）功能远比本示例复杂。

### 7.1 本示例简化了什么

| 项目 | 本示例 | ET 原版 |
| --- | --- | --- |
| 基础设施包 | 0 个（不依赖任何 cn.etetet.* 包） | 21 个基础设施包（Core / ECS / Network / Mongo / Actor 等） |
| Fiber 机制 | 无 | 有（多线程调度，每个 Fiber 独立 MainThread） |
| 热更机制 | 无（只演示 asmref 切换） | HybridCLR 热更 Hotfix 层 |
| 配置生成 | 无 Proto / Luban | 完整 Proto + Luban 流程 |
| 业务包数量 | 1 个（helloworld） | 多个业务包，通过 manifest.json 互相引用 |
| 切换工具 | PowerShell + 简单 C# 控制台 | `CodeModeChangeHelper.cs` dotnet 控制台程序，Unity 菜单 `ET/Init/CodeMode` 调用 |
| Loader 程序集 | 无 | 有（客户端启动时加载 Hotfix dll） |
| 服务端运行 | 仅演示编译产物 | 完整 .NET 服务端，可独立部署 |

### 7.2 ET 原版多了什么

#### (1) 多业务包通过 manifest.json 引用

ET 原版支持任意数量的业务包，每个业务包都有自己的 `manifest.json` 描述依赖关系，主包通过包管理器加载所有业务包的 asmref。

#### (2) CodeModeChangeHelper 是 dotnet 控制台程序

ET 原版的 `CodeModeChangeHelper.cs` 位于 `Packages/com.etetet.init/DotNet~/` 目录下，是一个独立的 .NET 控制台项目。Unity 编辑器菜单 `ET/Init/CodeMode` 会调用 `dotnet run` 执行它，而不是直接在 Unity 内执行 C# 脚本。这样做的好处是：

- 切换逻辑独立于 Unity，可在 CI/CD 中调用
- 避免 Unity 编辑器进程在切换过程中锁定文件
- 与服务端构建流程共享同一份代码

ET 原版的 `CodeModeChangeHelper.cs` 内部维护了一个 `Dictionary<string, HashSet<string>>` 规则表（对应 v 变量），规则比本示例更复杂，覆盖 6 个 ModelDir × 3 个 ServerDir × N 个业务包。

#### (3) 6 个程序集 × 多个业务包

ET 原版的规则表需要遍历所有业务包，对每个业务包的每个 `Model/Hotfix/ModelView/HotfixView/Config/Editor` 目录都应用 CodeMode 规则。本示例只演示 1 个业务包，规则更简洁。

#### (4) HybridCLR 热更 Hotfix 层

ET 原版使用 HybridCLR 对 `ET.Hotfix.dll` 和 `ET.HotfixView.dll` 做热更。这就是为什么 Hotfix 层必须 `noEngineReferences:true`——HybridCLR 加载的 dll 需要与 Unity 主工程解耦。

### 7.3 对应关系表

| 本示例组件 | ET 原版组件 | 说明 |
| --- | --- | --- |
| `Packages/cn.codemode.demo/Runtime/*/ET.*.asmdef` | `Packages/com.etetet.*.Runtime/*/ET.*.asmdef` | 6 个程序集定义，结构一致 |
| `Packages/cn.codemode.helloworld/Scripts/` | 各业务包 `Scripts/` 目录 | 业务代码按 Share/Client/Server 划分 |
| `Tools/Switch-CodeMode.ps1` | `Packages/com.etetet.init/DotNet~/CodeModeChangeHelper.cs` | 切换工具，本示例用 PowerShell 实现，ET 原版用 C# dotnet 程序 |
| `Tools/Verify-CodeMode.ps1` | 无对应 | 本示例新增的验证脚本，方便新手自查 |
| `Tools/CodeModeChangeHelper/` | `Packages/com.etetet.init/DotNet~/CodeModeChangeHelper/` | C# 版本切换工具，本示例简化后逻辑一致 |
| `AssemblyReference.asmref` 内容 `{ "reference": "ET.Hotfix" }` | 完全一致 | asmref 文件格式与 ET 原版 100% 一致 |
| `noEngineReferences:true` 设置 | 完全一致 | Hotfix / HotfixView / Config 都设 true |
| `Scripts/{Model,Hotfix,ModelView,HotfixView}/{Share,Client,Server}` | 完全一致 | 目录约定与 ET 原版一致 |
| 无 Loader 程序集 | `ET.Loader` 程序集 | ET 原版有 Loader 用于客户端启动时加载 Hotfix dll |
| 无 Fiber 机制 | `ET.Fiber` 机制 | ET 原版有多 Fiber 调度，本示例不涉及 |

---

## 第8章 常见疑问 FAQ

### Q1: 为什么 Hotfix 要设 `noEngineReferences:true`？

**A:** Hotfix 层是**热更代码**，需要满足两个条件：
1. 在不依赖 UnityEngine 的情况下编译——这样服务端纯 .NET 进程也能加载 Hotfix dll 运行业务逻辑。
2. 方便 HybridCLR 加载——HybridCLR 热更的 dll 需要与 Unity 主工程解耦，`noEngineReferences:true` 让 Hotfix dll 编译时不会硬依赖 UnityEngine.dll。

如果 Hotfix 引用了 UnityEngine，服务端部署时就需要带上 Unity 引擎，失去纯 .NET 部署的优势。

### Q2: 为什么 Server 不能引用 UnityEngine？

**A:** 服务端是**纯 .NET 进程**（Linux 上 `dotnet run` 启动），没有 Unity 引擎运行时。如果 Server 代码 `using UnityEngine;`：
- 编译时找不到 `UnityEngine.dll`（因为服务端工程不引用 Unity 程序集），直接编译失败。
- 即使强行编译过，运行时调用 `UnityEngine.Vector3` 等类型会找不到程序集，进程崩溃。

因此 Server 目录的代码必须只使用 .NET BCL（基础类库）和 Share 层类型。本示例 `ServerData.cs` 和 `ServerHelloWorldSystem.cs` 都只用了 `System` 命名空间。

### Q3: 为什么需要 Share 层？

**A:** 共享数据类型和接口，避免客户端和服务端重复定义。

典型场景：同一份战斗逻辑，客户端用于表现层预测（先显示一次结果让玩家感觉流畅），服务端用于权威计算（最终判定结果）。如果战斗公式只在客户端，服务端没法校验；只在服务端，客户端没法预测。Share 层让双方共用同一份 `BattleSystem.CalculateDamage()` 源码，编译时各自编进 Client dll 和 Server dll。

本示例的 `HelloWorldBase`（Share 层数据）和 `HelloWorldSystem`（Share 层逻辑）就是这种共享代码，Client 和 Server 模式下都会被编译。

### Q4: ModelView 和 HotfixView 是干嘛的？

**A:** 是**表现层程序集**。ET 把代码分为纯逻辑层和表现层：

| 程序集 | 层次 | 是否含 UnityEngine | 是否可热更 |
| --- | --- | --- | --- |
| `Model` | 纯逻辑层数据 | 可引用 | 否 |
| `Hotfix` | 纯逻辑层逻辑 | 否（noEngineReferences） | 是 |
| `ModelView` | 表现层数据 | 是（UI/动画/特效组件） | 否 |
| `HotfixView` | 表现层逻辑 | 否（noEngineReferences） | 是 |

- `Model/Hotfix` 跑在客户端和服务端（逻辑层共用）。
- `ModelView/HotfixView` 只在客户端跑（含 UI、动画、特效等表现层代码）。
- 服务端通常没有 `ModelView/HotfixView` 代码（本示例为演示完整结构，放了 `ServerViewSystem.cs`，但实际服务端"表现层"用于场景对象管理而非 UI）。

### Q5: asmref 文件删除后，目录下的代码还会被编译吗？

**A:** **不会**。Unity 编译时只编译有 asmref 指向 asmdef 的目录代码。

这是 Unity 的硬规则：
- 一个目录下若有 `AssemblyReference.asmref`，该目录的 .cs 会被编译到 asmref 指向的程序集。
- 一个目录下若**没有** asmref，但上级目录有 asmdef，则会被编译到上级 asmdef。
- 一个目录下若**既没有 asmref 也没有上级 asmdef**，则该目录的 .cs **完全不参与编译**（Unity 会忽略它们，连语法错误都不会报）。

CodeMode 切换的精髓就在于此：**删除 asmref = 该目录代码不参与编译**，源码仍留在硬盘上不丢，下次切换回来又会被编译。

### Q6: 切换 CodeMode 后还需要做什么？

**A:** 大多数情况下**什么都不用做**，Unity 会自动完成：

1. Unity 编辑器会监测到文件系统变化（asmref 文件增删）。
2. 自动触发 `CompilationPipeline` 重新编译。
3. 编译完成后刷新 AssetDatabase。

如果是 ET 原版，还会额外触发：
- `dotnet build ET.sln` 重新生成服务端 dll（供 Server 模式部署用）。
- 重新生成 Proto / Luban 配置代码（如果协议有变动）。
- 刷新 HybridCLR 的 AOT dll 列表。

本示例因为不涉及服务端独立部署和热更，切换后只需等 Unity 重编译完成即可。

### Q7: 为什么本示例没有 Loader 程序集？

**A:** `Loader` 是 ET 的**特殊程序集**，用于客户端启动时加载 Hotfix dll。它的工作流程：

1. Unity 启动时，先运行 `ET.Loader` 中的引导代码。
2. `Loader` 从磁盘加载 `ET.Hotfix.dll` / `ET.HotfixView.dll`（HybridCLR 编译产物）。
3. 调用 Hotfix 层的入口方法，开始游戏逻辑。

本示例是**纯演示**，目的是让新手看懂 asmdef/asmref 机制，不涉及实际运行时加载流程，所以没有 Loader。如果要做真正的热更客户端，需要参考 ET 原版的 Loader 实现。

---

## 第9章 动手练习

本章带你亲手执行 3 种 CodeMode 切换，观察 `AssemblyReference.asmref` 文件的分布变化。

### 9.1 准备工作

打开 PowerShell（建议 PowerShell 7+），进入 Tools 目录：

```powershell
cd d:\Unity\LockstepDemo\CodeMode\Tools
```

确保以下文件存在：
- `Switch-CodeMode.ps1` —— 切换脚本
- `Verify-CodeMode.ps1` —— 验证脚本
- 业务包目录 `..\Packages\cn.codemode.helloworld\Scripts\` 存在

### 9.2 练习 1：切换到 Client 模式

```powershell
cd d:\Unity\LockstepDemo\CodeMode\Tools

# 切换到 Client 模式
.\Switch-CodeMode.ps1 -CodeMode Client

# 验证切换结果
.\Verify-CodeMode.ps1 -CodeMode Client
```

**预期输出**（Verify 脚本）：

```
[Verify-CodeMode] CodeMode = Client
[PASS] Scripts/Model/Share/AssemblyReference.asmref 存在
[PASS] Scripts/Model/Client/AssemblyReference.asmref 存在
[PASS] Scripts/Model/Server/AssemblyReference.asmref 不存在
[PASS] Scripts/Hotfix/Share/AssemblyReference.asmref 存在
[PASS] Scripts/Hotfix/Client/AssemblyReference.asmref 存在
[PASS] Scripts/Hotfix/Server/AssemblyReference.asmref 不存在
...
[Result] 全部 PASS，CodeMode = Client 切换成功
```

**观察重点**：
- `Scripts/Model/Server/ServerData.cs` 还在硬盘上，但目录下**没有 asmref**，因此 Unity 不会编译它。
- `Scripts/Hotfix/Client/ClientHelloWorldSystem.cs` 目录下**有 asmref**，会被编译到 `ET.Hotfix.dll`。

### 9.3 练习 2：切换到 Server 模式

```powershell
# 切换到 Server 模式
.\Switch-CodeMode.ps1 -CodeMode Server

# 验证切换结果
.\Verify-CodeMode.ps1 -CodeMode Server
```

**预期输出**：

```
[Verify-CodeMode] CodeMode = Server
[PASS] Scripts/Model/Share/AssemblyReference.asmref 存在
[PASS] Scripts/Model/Client/AssemblyReference.asmref 不存在
[PASS] Scripts/Model/Server/AssemblyReference.asmref 存在
[PASS] Scripts/Hotfix/Share/AssemblyReference.asmref 存在
[PASS] Scripts/Hotfix/Client/AssemblyReference.asmref 不存在
[PASS] Scripts/Hotfix/Server/AssemblyReference.asmref 存在
...
[Result] 全部 PASS，CodeMode = Server 切换成功
```

**观察重点**：
- 这次 `Scripts/Model/Client/ClientData.cs` 目录下**没有 asmref**，`ClientData` 类不会参与编译。
- Server 目录的 `ServerData.cs` / `ServerHelloWorldSystem.cs` 都有 asmref，会被编译到 `ET.Model.dll` / `ET.Hotfix.dll`。

### 9.4 练习 3：切换到 ClientServer 模式

```powershell
# 切换到 ClientServer 模式
.\Switch-CodeMode.ps1 -CodeMode ClientServer

# 验证切换结果
.\Verify-CodeMode.ps1 -CodeMode ClientServer
```

**预期输出**：

```
[Verify-CodeMode] CodeMode = ClientServer
[PASS] Scripts/Model/Share/AssemblyReference.asmref 存在
[PASS] Scripts/Model/Client/AssemblyReference.asmref 存在
[PASS] Scripts/Model/Server/AssemblyReference.asmref 存在
[PASS] Scripts/Hotfix/Share/AssemblyReference.asmref 存在
[PASS] Scripts/Hotfix/Client/AssemblyReference.asmref 存在
[PASS] Scripts/Hotfix/Server/AssemblyReference.asmref 存在
...
[Result] 全部 PASS，CodeMode = ClientServer 切换成功
```

**观察重点**：
- 所有子目录都有 asmref，所有 .cs 都参与编译。
- 这是开发期联调的默认模式。

### 9.5 也可以使用 C# 工具

如果想对照 ET 原版的 `CodeModeChangeHelper.cs` 学习，可以使用 C# 版本：

```powershell
cd d:\Unity\LockstepDemo\CodeMode

# 编译 C# 工具（首次执行需要）
dotnet build Tools/CodeModeChangeHelper

# 切换到 Client 模式
dotnet run --project Tools/CodeModeChangeHelper -- --CodeMode=Client

# 切换到 Server 模式
dotnet run --project Tools/CodeModeChangeHelper -- --CodeMode=Server

# 切换到 ClientServer 模式
dotnet run --project Tools/CodeModeChangeHelper -- --CodeMode=ClientServer
```

C# 工具的输出与 PowerShell 脚本一致，行为也完全一致——它存在的目的是让你对照 ET 原版源码理解切换逻辑。

### 9.6 观察技巧

每次切换后，建议观察 `Packages/cn.codemode.helloworld/Scripts/` 下的 `AssemblyReference.asmref` 文件分布。可以用 PowerShell 一行命令查看：

```powershell
# 查看当前所有 asmref 文件分布
Get-ChildItem -Path ..\Packages\cn.codemode.helloworld\Scripts\ -Recurse -Filter "AssemblyReference.asmref" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    Write-Host "$($_.FullName) -> $content"
}
```

或者用资源管理器打开 `Packages\cn.codemode.helloworld\Scripts\` 目录，按文件夹展开查看 asmref 文件的有无。

### 9.7 思考题

完成练习后，请思考以下问题：

1. **在 Client 模式下，`Scripts/Model/Server/ServerData.cs` 会被编译进 dll 吗？为什么？**
2. **如果想让服务端代码也能引用 UnityEngine，需要修改什么？**
3. **为什么 Share 目录在所有模式下都有 asmref？**

### 9.8 期望答案

**问题 1**：**不会**。因为 `Scripts/Model/Server/` 目录下没有 `AssemblyReference.asmref`，Unity 不会编译该目录代码。源码仍保留在硬盘上，下次切到 Server 或 ClientServer 模式时，asmref 重新生成后才会被编译。

**问题 2**：修改 `ET.Hotfix.asmdef`（或对应的 Server 代码所属 asmdef）的 `noEngineReferences` 为 `false`。**但这样会导致服务端无法纯 .NET 运行**（因为运行时找不到 UnityEngine.dll），不推荐这样做。正确做法是把需要 UnityEngine 的代码放到 `Client/` 目录而不是 `Server/` 目录。

**问题 3**：Share 是**共享代码**，必须同时被 Client 和 Server 编译。无论哪种 CodeMode，Share 目录的代码都需要参与编译，所以任何 CodeMode 下都需要 asmref 让代码被编译到对应程序集。如果 Share 目录没有 asmref，共享类型（如 `HelloWorldBase`）就消失了，Client 和 Server 代码都会因为找不到这些类型而编译失败。

---

## 结语

完成本示例的 3 个练习后，你应该已经直观理解了 ET 的两个核心机制：

1. **All-in-One 架构**：一个 Unity 工程通过 CodeMode 切换，产出客户端 dll 或服务端 dll，避免传统 3 项目的代码重复问题。
2. **asmdef + asmref 编译开关**：asmdef 定义程序集，asmref 声明代码归属，删除 asmref 即剔除编译——这就是 CodeMode 切换的底层原理。

下一步建议：
- 阅读 ET 原版源码 `Packages/com.etetet.init/DotNet~/CodeModeChangeHelper.cs`，对比本示例的 C# 工具。
- 在 ET 原版项目中执行 `ET/Init/CodeMode` 菜单，观察多业务包场景下的切换行为。
- 学习 HybridCLR 热更机制，理解 Hotfix 层为什么必须 `noEngineReferences:true`。
- 参考 ET 仓库中的 `Book/8.1ET Package制作指南.md` 和 `Book/8.2ET Package目录.md` 深入学习业务包组织方式。

祝你早日掌握 ET 框架，构建出自己的 All-in-One 联网游戏！
