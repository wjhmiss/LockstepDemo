# ET All-in-One + CodeMode 架构新手示例 Spec

## Why
新手对 ET 的 All-in-One + CodeMode 架构难以理解：一个 Unity 项目如何在三种模式（Client/Server/ClientServer）下编译出不同的产物。需要一个**最小化、可独立运行、不依赖完整 ET 包**的示例，通过实际目录结构 + 可执行切换脚本 + 详尽文档，让新手直观看到 asmdef/asmref 的生成与切换原理。

## What Changes
- 在 `d:\Unity\LockstepDemo\CodeMode` 创建一个**独立的**、**不依赖 ET 完整基础设施包**的最小化 Unity 项目结构示例
- 演示三种 CodeMode（Client / Server / ClientServer）下 `AssemblyReference.asmref` 文件的生成与删除逻辑
- 提供一个 PowerShell 切换脚本 `Switch-CodeMode.ps1`，模拟 ET 的 `CodeModeChangeHelper.cs` 行为
- 提供 6 个 asmdef（ET.Model / ET.Hotfix / ET.ModelView / ET.HotfixView / ET.Config / ET.Editor），由业务包通过 asmref 引用
- 提供 3 套示例代码：
  - `Share/`：共享代码（HelloWorld 基类）
  - `Client/`：客户端特有代码（ClientHelloWorld）
  - `Server/`：服务端特有代码（ServerHelloWorld）
- 提供一份详尽的 `README.md` 说明文档，覆盖：CodeMode 概念、asmdef/asmref 原理、三种模式对比、切换流程、与 ET 原版的对应关系、常见疑问

## Impact
- 参考文档：[ET架构设计与程序集机制详解.md](file:///d:/Unity/LockstepDemo/ET/ET架构设计与程序集机制详解.md)、[ET新手学习教程.md](file:///d:/Unity/LockstepDemo/ET/ET新手学习教程.md)
- 参考源码：[CodeModeChangeHelper.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/DotNet~/CodeModeChangeHelper.cs)、[CodeModeEditor.cs](file:///d:/Unity/LockstepDemo/ET/Packages/com.etetet.init/Editor/CodeModeEditor.cs)
- 产出目录：`d:\Unity\LockstepDemo\CodeMode`
- 不修改 ET 原项目，不依赖 Asystem 项目，可独立打开运行

## ADDED Requirements

### Requirement: 独立的最小化项目骨架
系统 SHALL 在 `d:\Unity\LockstepDemo\CodeMode` 下创建一个不依赖 ET 包的最小化 Unity 项目结构（仅包含必要的 `Packages/manifest.json`、`ProjectSettings/ProjectVersion.txt`、`Assets/` 目录），用于演示 CodeMode 机制。

#### Scenario: 项目可独立打开
- **WHEN** 用户用 Unity 2022.3 打开 `d:\Unity\LockstepDemo\CodeMode`
- **THEN** 项目可正常打开，无包缺失错误
- **AND** 不引用任何 ET 完整业务包

### Requirement: 主包定义6个asmdef
系统 SHALL 在主包 `cn.codemode.demo/Runtime/` 下定义 6 个 asmdef 文件，与 ET 规范一致：
- `Model/ET.Model.asmdef`
- `Hotfix/ET.Hotfix.asmdef`（设 `noEngineReferences:true`）
- `ModelView/ET.ModelView.asmdef`
- `HotfixView/ET.HotfixView.asmdef`（设 `noEngineReferences:true`）
- `Config/ET.Config.asmdef`（设 `noEngineReferences:true`）
- `Editor/ET.Editor.asmdef`

#### Scenario: asmdef 配置正确
- **WHEN** 读取 `Runtime/Hotfix/ET.Hotfix.asmdef`
- **THEN** `noEngineReferences` 字段为 `true`
- **AND** `name` 字段为 `"ET.Hotfix"`

### Requirement: 业务包目录结构演示Share/Client/Server划分
系统 SHALL 在业务包 `cn.codemode.helloworld/Scripts/` 下按 `{Model,Hotfix,ModelView,HotfixView}/{Share,Client,Server}` 组织代码目录，每个目录下放置示例 .cs 文件，演示三种代码归属：
- `Scripts/Model/Share/HelloWorldBase.cs` — 共享数据模型
- `Scripts/Model/Client/ClientData.cs` — 仅客户端编译
- `Scripts/Model/Server/ServerData.cs` — 仅服务端编译
- `Scripts/Hotfix/Share/HelloWorldSystem.cs` — 共享逻辑
- `Scripts/Hotfix/Client/ClientHelloWorldSystem.cs` — 客户端逻辑
- `Scripts/Hotfix/Server/ServerHelloWorldSystem.cs` — 服务端逻辑

#### Scenario: 三套代码并存
- **WHEN** 检查 `cn.codemode.helloworld/Scripts/`
- **THEN** Share/Client/Server 三套目录都存在示例代码
- **AND** Client 目录代码引用了 `UnityEngine`，Server 目录代码不引用 `UnityEngine`

### Requirement: PowerShell 切换脚本
系统 SHALL 提供 `Switch-CodeMode.ps1` 脚本，参数为 ` -CodeMode <Client|Server|ClientServer>`，模拟 ET 的 `CodeModeChangeHelper.cs` 行为：
- 删除业务包所有目录下的 `AssemblyReference.asmref` 文件
- 根据 CodeMode 在指定目录下生成 `AssemblyReference.asmref`，文件内容为 `{ "reference": "ET.<Model|Hotfix|ModelView|HotfixView|Config|Editor>" }`
- 控制台输出每一步的删除/创建日志

#### Scenario: 切换到 Client 模式
- **WHEN** 执行 `./Switch-CodeMode.ps1 -CodeMode Client`
- **THEN** `Scripts/Model/Server/AssemblyReference.asmref` 被删除
- **AND** `Scripts/Model/Client/AssemblyReference.asmref` 内容为 `{ "reference": "ET.Model" }`
- **AND** `Scripts/Model/Share/AssemblyReference.asmref` 内容为 `{ "reference": "ET.Model" }`
- **AND** `Scripts/Hotfix/Server/AssemblyReference.asmref` 不存在

#### Scenario: 切换到 Server 模式
- **WHEN** 执行 `./Switch-CodeMode.ps1 -CodeMode Server`
- **THEN** `Scripts/Model/Client/AssemblyReference.asmref` 被删除
- **AND** `Scripts/Model/Server/AssemblyReference.asmref` 内容为 `{ "reference": "ET.Model" }`
- **AND** `Scripts/Model/Share/AssemblyReference.asmref` 内容为 `{ "reference": "ET.Model" }`

#### Scenario: 切换到 ClientServer 模式
- **WHEN** 执行 `./Switch-CodeMode.ps1 -CodeMode ClientServer`
- **THEN** Client 和 Server 目录下都有 `AssemblyReference.asmref` 文件
- **AND** Share 目录下也有 `AssemblyReference.asmref` 文件

### Requirement: 验证脚本
系统 SHALL 提供 `Verify-CodeMode.ps1` 脚本，参数为 ` -CodeMode <Client|Server|ClientServer>`，用于验证切换结果是否符合预期，输出每项检查的 PASS/FAIL。

#### Scenario: 验证 Client 模式
- **WHEN** 执行 `./Verify-CodeMode.ps1 -CodeMode Client`（在切换到 Client 后）
- **THEN** 所有 Client+Share 目录的 asmref 存在性检查输出 PASS
- **AND** 所有 Server 目录的 asmref 不存在性检查输出 PASS

### Requirement: 详尽说明文档
系统 SHALL 在 `d:\Unity\LockstepDemo\CodeMode\README.md` 中提供详尽说明，覆盖以下主题：
1. **什么是 All-in-One 架构**：ET 为什么不用三个独立项目
2. **什么是 CodeMode**：三种模式的定义与使用场景
3. **asmdef 与 asmref 原理**：包含图示和示例
4. **目录约定**：`Scripts/{Model,Hotfix,ModelView,HotfixView}/{Share,Client,Server}` 的含义
5. **切换流程**：从命令行执行到 Unity 编译产物的完整流程
6. **三种模式产物对比表**：哪些 asmref 存在、哪些 .cs 被编译、最终 dll 差异
7. **与 ET 原版的对应关系**：本示例简化了什么、ET 原版多了什么
8. **常见疑问 FAQ**：为什么 noEngineReferences？为什么 Server 不能引用 UnityEngine？为什么需要 Share？

#### Scenario: 文档完整
- **WHEN** 打开 `README.md`
- **THEN** 文档包含上述 8 个主题章节
- **AND** 每个章节有代码示例或目录结构图示
- **AND** 文档末尾有"动手练习"小节，指导新手依次执行 3 种模式并验证

### Requirement: 简化的 CodeModeChangeHelper C# 实现
系统 SHALL 在 `Tools/CodeModeChangeHelper.cs` 提供一个简化的 C# 实现（**纯命令行可执行**，不依赖 UnityEngine），让新手对照 ET 原版源码理解逻辑。该实现包含一个 `Main` 方法，参数为 `--CodeMode=<Client|Server|ClientServer>`，逻辑与 PowerShell 脚本一致。

#### Scenario: C# 工具可编译运行
- **WHEN** 执行 `dotnet run --project Tools/CodeModeChangeHelper -- --CodeMode=Client`
- **THEN** 程序输出与 PowerShell 脚本一致的切换日志
- **AND** 退出码为 0

### Requirement: 不依赖 ET 完整包
系统 SHALL 保证示例项目不通过 manifest.json 引用任何 `cn.etetet.*` 包，所有 asmdef/asmref 机制完全独立演示。允许引用少量 Unity 内置包（如 `com.unity.ugui`）但不强制。

#### Scenario: manifest.json 干净
- **WHEN** 读取 `Packages/manifest.json`
- **THEN** 不存在任何 `cn.etetet.*` 的依赖
- **AND** 依赖列表最小化
