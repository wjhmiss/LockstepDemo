# Tasks

- [x] Task 1: 创建最小化 Unity 项目骨架
  - [x] SubTask 1.1: 在 `d:\Unity\LockstepDemo\CodeMode` 下创建 `Packages/manifest.json`（不引用任何 cn.etetet.* 包）、`ProjectSettings/ProjectVersion.txt`（`m_EditorVersion: 2022.3.62f1`）、`Assets/.gitkeep`、`.gitignore`
  - [x] SubTask 1.2: 创建 `Directory.Build.props`（配置统一编译选项）

- [x] Task 2: 创建主包 cn.codemode.demo（定义 6 个 asmdef）
  - [x] SubTask 2.1: 创建 `Packages/cn.codemode.demo/package.json`
  - [x] SubTask 2.2: 在 `Runtime/` 下创建 6 个 asmdef：
    - `Model/ET.Model.asmdef`（name=ET.Model）
    - `Hotfix/ET.Hotfix.asmdef`（name=ET.Hotfix，noEngineReferences=true，references=[ET.Model]）
    - `ModelView/ET.ModelView.asmdef`（name=ET.ModelView，references=[ET.Model]）
    - `HotfixView/ET.HotfixView.asmdef`（name=ET.HotfixView，noEngineReferences=true，references=[ET.ModelView, ET.Hotfix]）
    - `Config/ET.Config.asmdef`（name=ET.Config，noEngineReferences=true）
    - `Editor/ET.Editor.asmdef`（name=ET.Editor，references=[ET.Model]）

- [x] Task 3: 创建业务包 cn.codemode.helloworld（Share/Client/Server 示例代码）
  - [x] SubTask 3.1: 创建 `Packages/cn.codemode.helloworld/package.json`
  - [x] SubTask 3.2: 创建 `Scripts/Model/Share/HelloWorldBase.cs`（共享数据模型，含 Name/Id 字段，可被 Client 和 Server 引用）
  - [x] SubTask 3.3: 创建 `Scripts/Model/Client/ClientData.cs`（客户端特有数据，引用 UnityEngine）
  - [x] SubTask 3.4: 创建 `Scripts/Model/Server/ServerData.cs`（服务端特有数据，不引用 UnityEngine）
  - [x] SubTask 3.5: 创建 `Scripts/Hotfix/Share/HelloWorldSystem.cs`（共享逻辑：SayHello 方法）
  - [x] SubTask 3.6: 创建 `Scripts/Hotfix/Client/ClientHelloWorldSystem.cs`（客户端逻辑：含 Debug.Log）
  - [x] SubTask 3.7: 创建 `Scripts/Hotfix/Server/ServerHelloWorldSystem.cs`（服务端逻辑：含 Console.WriteLine）
  - [x] SubTask 3.8: 创建 `Scripts/HotfixView/Client/ClientViewSystem.cs`（演示 ModelView/HotfixView 用途）
  - [x] SubTask 3.9: 创建 `Scripts/HotfixView/Server/ServerViewSystem.cs`

- [x] Task 4: 实现 PowerShell 切换脚本 Switch-CodeMode.ps1
  - [x] SubTask 4.1: 创建 `Tools/Switch-CodeMode.ps1`，参数 `-CodeMode <Client|Server|ClientServer>`
  - [x] SubTask 4.2: 实现切换逻辑：先删除所有 `Scripts/**/AssemblyReference.asmref`，再按 CodeMode 规则生成新的 asmref 文件
  - [x] SubTask 4.3: 实现切换规则表（与 ET 原版一致，覆盖 Model/Hotfix/ModelView/HotfixView/Config/Editor × Share/Client/Server）
  - [x] SubTask 4.4: 输出每一步的删除/创建日志

- [x] Task 5: 实现 PowerShell 验证脚本 Verify-CodeMode.ps1
  - [x] SubTask 5.1: 创建 `Tools/Verify-CodeMode.ps1`，参数 `-CodeMode <Client|Server|ClientServer>`
  - [x] SubTask 5.2: 针对当前 CodeMode 检查所有预期的 asmref 文件存在性，输出 PASS/FAIL
  - [x] SubTask 5.3: 检查所有不应存在的 asmref 文件确实不存在

- [x] Task 6: 实现简化的 C# CodeModeChangeHelper
  - [x] SubTask 6.1: 创建 `Tools/CodeModeChangeHelper/CodeModeChangeHelper.csproj`（net8.0 控制台项目）
  - [x] SubTask 6.2: 实现 `CodeModeChangeHelper.cs`，参数 `--CodeMode=<Client|Server|ClientServer>`，逻辑与 PowerShell 脚本一致
  - [x] SubTask 6.3: 输出与 ET 原版风格一致的日志

- [x] Task 7: 编写详尽的 README.md 说明文档
  - [x] SubTask 7.1: 编写 `README.md` 第1章：什么是 All-in-One 架构
  - [x] SubTask 7.2: 编写第2章：什么是 CodeMode（三种模式定义）
  - [x] SubTask 7.3: 编写第3章：asmdef 与 asmref 原理（含目录结构图示）
  - [x] SubTask 7.4: 编写第4章：目录约定（Scripts/{Model,Hotfix,ModelView,HotfixView}/{Share,Client,Server}）
  - [x] SubTask 7.5: 编写第5章：切换流程（命令行 → asmref 文件 → Unity 编译 → 产物）
  - [x] SubTask 7.6: 编写第6章：三种模式产物对比表
  - [x] SubTask 7.7: 编写第7章：与 ET 原版的对应关系
  - [x] SubTask 7.8: 编写第8章：常见疑问 FAQ
  - [x] SubTask 7.9: 编写第9章：动手练习（3 种模式依次执行并验证）

- [x] Task 8: 验证示例可运行
  - [x] SubTask 8.1: 验证 PowerShell 脚本可执行：`./Tools/Switch-CodeMode.ps1 -CodeMode Client` 后 asmref 文件正确生成
  - [x] SubTask 8.2: 验证 Verify 脚本：3 种模式下均输出全部 PASS（Client/Server/ClientServer 各 12 PASS / 0 FAIL）
  - [x] SubTask 8.3: 验证 C# 工具可编译：`dotnet build Tools/CodeModeChangeHelper`
  - [x] SubTask 8.4: 验证 C# 工具可运行：`dotnet run --project Tools/CodeModeChangeHelper -- --CodeMode=ClientServer` 与 PowerShell 脚本输出一致（均创建 12 个 asmref）
  - [x] SubTask 8.5: 验证 README.md 包含 9 个章节（grep `^##\s+第\d+章` 命中 9 行）

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 2]
- [Task 4] depends on [Task 3]
- [Task 5] depends on [Task 4]
- [Task 6] depends on [Task 4]
- [Task 7] depends on [Task 4]
- [Task 8] depends on [Task 5, Task 6, Task 7]
