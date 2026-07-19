# Checklist

## 项目骨架
- [x] `d:\Unity\LockstepDemo\CodeMode` 目录存在
- [x] `Packages/manifest.json` 存在且不引用任何 `cn.etetet.*` 包
- [x] `ProjectSettings/ProjectVersion.txt` 存在，内容为 `m_EditorVersion: 2022.3.62f1`
- [x] `Assets/` 目录存在（含 .gitkeep）
- [x] `.gitignore` 存在
- [x] `Directory.Build.props` 存在

## 主包 asmdef 定义
- [x] `Packages/cn.codemode.demo/package.json` 存在
- [x] `Runtime/Model/ET.Model.asmdef` 存在，name="ET.Model"
- [x] `Runtime/Hotfix/ET.Hotfix.asmdef` 存在，name="ET.Hotfix"，noEngineReferences=true
- [x] `Runtime/ModelView/ET.ModelView.asmdef` 存在，name="ET.ModelView"
- [x] `Runtime/HotfixView/ET.HotfixView.asmdef` 存在，name="ET.HotfixView"，noEngineReferences=true
- [x] `Runtime/Config/ET.Config.asmdef` 存在，name="ET.Config"，noEngineReferences=true
- [x] `Runtime/Editor/ET.Editor.asmdef` 存在，name="ET.Editor"

## 业务包目录结构
- [x] `Packages/cn.codemode.helloworld/package.json` 存在
- [x] `Scripts/Model/Share/HelloWorldBase.cs` 存在，不引用 UnityEngine
- [x] `Scripts/Model/Client/ClientData.cs` 存在，引用 UnityEngine
- [x] `Scripts/Model/Server/ServerData.cs` 存在，不引用 UnityEngine
- [x] `Scripts/Hotfix/Share/HelloWorldSystem.cs` 存在
- [x] `Scripts/Hotfix/Client/ClientHelloWorldSystem.cs` 存在，含 Debug.Log
- [x] `Scripts/Hotfix/Server/ServerHelloWorldSystem.cs` 存在，含 Console.WriteLine
- [x] `Scripts/HotfixView/Client/ClientViewSystem.cs` 存在
- [x] `Scripts/HotfixView/Server/ServerViewSystem.cs` 存在

## PowerShell 切换脚本
- [x] `Tools/Switch-CodeMode.ps1` 存在，参数 `-CodeMode <Client|Server|ClientServer>`
- [x] 执行 `-CodeMode Client` 后，Client+Share 目录下有 asmref 文件，Server 目录下无 asmref 文件（8 个 asmref）
- [x] 执行 `-CodeMode Server` 后，Server+Share 目录下有 asmref 文件，Client 目录下无 asmref 文件（8 个 asmref）
- [x] 执行 `-CodeMode ClientServer` 后，Client+Server+Share 目录下均有 asmref 文件（12 个 asmref）
- [x] 生成的 asmref 文件内容格式为 `{ "reference": "ET.<Model|Hotfix|ModelView|HotfixView|Config|Editor>" }`
- [x] 切换日志输出每一步删除/创建操作

## PowerShell 验证脚本
- [x] `Tools/Verify-CodeMode.ps1` 存在，参数 `-CodeMode <Client|Server|ClientServer>`
- [x] 执行 `-CodeMode Client`（在 Client 模式下）输出全部 PASS（12 PASS / 0 FAIL）
- [x] 执行 `-CodeMode Server`（在 Server 模式下）输出全部 PASS（12 PASS / 0 FAIL）
- [x] 执行 `-CodeMode ClientServer`（在 ClientServer 模式下）输出全部 PASS（12 PASS / 0 FAIL）

## C# 工具
- [x] `Tools/CodeModeChangeHelper/CodeModeChangeHelper.csproj` 存在，target net8.0
- [x] `Tools/CodeModeChangeHelper/CodeModeChangeHelper.cs` 存在，含 Main 方法
- [x] `dotnet build Tools/CodeModeChangeHelper` 编译通过（生成 ET.CodeMode.dll）
- [x] `dotnet run --project Tools/CodeModeChangeHelper -- --CodeMode=ClientServer` 可执行，输出切换日志
- [x] C# 工具执行结果与 PowerShell 脚本一致（ClientServer 模式均创建 12 个 asmref）

## README.md 说明文档
- [x] `README.md` 存在
- [x] 第1章：什么是 All-in-One 架构
- [x] 第2章：什么是 CodeMode（三种模式定义）
- [x] 第3章：asmdef 与 asmref 原理（含目录结构图示）
- [x] 第4章：目录约定（Scripts/{Model,Hotfix,ModelView,HotfixView}/{Share,Client,Server}）
- [x] 第5章：切换流程（命令行 → asmref 文件 → Unity 编译 → 产物）
- [x] 第6章：三种模式产物对比表
- [x] 第7章：与 ET 原版的对应关系
- [x] 第8章：常见疑问 FAQ
- [x] 第9章：动手练习（3 种模式依次执行并验证）

## 整体验证
- [x] 项目目录结构清晰，与 README 中描述一致
- [x] 3 种 CodeMode 切换脚本均可成功执行
- [x] C# 工具与 PowerShell 脚本行为一致
- [x] README.md 文档完整，覆盖 9 个章节

## 备注
- PowerShell 脚本通过 `pwsh`（PowerShell 7+）调用验证通过。若使用 `powershell.exe`（5.1）调用，因 Trae 终端的 safe_rm_aliases.ps1 wrapper 会破坏 `$` 变量符号导致脚本失败，建议在标准 PowerShell 5.1 环境或 PowerShell 7+ 环境下运行。
- C# 工具的 csproj 中追加了 `<LangVersion>latest</LangVersion>` 以覆盖上层 `Directory.Build.props` 的 `LangVersion=9.0`，避免与 `<ImplicitUsings>enable</ImplicitUsings>` 冲突。
- 业务包 `cn.codemode.helloworld` 当前为 ClientServer 模式状态（保留 12 个 asmref 文件），可用作初始查看状态。
