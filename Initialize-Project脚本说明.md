# Initialize-Project.ps1 脚本说明文档

> 适用版本：ET 框架（ET10 "昭君"）
> 脚本路径：`ET-master/Scripts/Initialize-Project.ps1`
> 适用读者：ET 框架新手、运维人员、CI/CD 接入人员

---

## 一、脚本定位

`Initialize-Project.ps1` 是 ET 框架的**命令行初始化脚本**，用于替代 Unity 菜单中 "ET/Compile" 流程。

它把项目从"代码仓库状态"准备好到"可以在 Unity 中打开运行"的状态。

**为什么需要这个脚本：**
- ET 框架是基于 Unity Package 的模块化架构，每次开发前需要确定"主包"（决定游戏入口）
- ET 支持 Client / Server / ClientServer 三种代码模式，需要工具切换
- ET 依赖 Luban（配置表工具）和 SourceGenerator（源生成器）等工具，需要先编译
- 这些步骤手动操作容易出错，所以用脚本自动化

---

## 二、命令行参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `-ProjectRoot` | string | 脚本上一级目录 | 项目根目录路径 |
| `-SceneName` | string | `''` | 场景名，用于推导主包名；留空时从 `GlobalConfig.asset` 读取 |
| `-MainPackage` | string | `''` | 主包名（格式 `cn.etetet.{SceneName 小写}`）；留空时由 `SceneName` 推导 |
| `-CodeMode` | enum | `''` | 代码模式：`Client` / `Server` / `ClientServer` |
| `-GenerateMainPackageOnly` | switch | - | 仅生成主包文件，跳过后续构建 |
| `-SkipLubanBuild` | switch | - | 跳过 Luban 编译 |
| `-SkipToolBuild` | switch | - | 跳过 ET.CodeMode 和 ET.SourceGenerator 编译 |
| `-SkipCodeMode` | switch | - | 跳过执行 ET.CodeMode.dll（即不切换代码模式） |
| `-SkipFullBuild` | switch | - | 跳过 ET.sln 完整编译（仅与 `-BuildSolution` 联用生效） |
| `-BuildSolution` | switch | - | 在工具构建完成后额外编译整个 ET.sln |
| `-DotNetSdkVersions` | string[] | `@()` | 手动传入的 .NET SDK 版本列表（一般留空自动检测） |
| `-GitExecutable` | string | `'git'` | git 可执行文件路径或命令名 |

### CodeMode 三种模式

| 值 | 含义 | 适用场景 |
|----|------|----------|
| `Client` | 仅客户端代码 | 客户端独立打包 |
| `Server` | 仅服务端代码 | 服务端独立部署 |
| `ClientServer` | 双端代码（默认） | 本地开发联调 |

---

## 三、ET 框架基础概念速览（新手必读）

在理解脚本流程之前，先了解 ET 框架的几个核心概念。这些概念是理解后续每一步作用的基础。

### 1. Unity Package 包结构

ET 项目所有代码都放在 `Packages/cn.etetet.*` 目录下，每个目录是一个 Unity Package：
- 包名以 `cn.etetet.` 为前缀（ET 框架命名规范，违反则脚本报"非法主包名"错误）
- 每个包有自己的 `package.json`，声明包名、版本、依赖
- 例如：`cn.etetet.core`（核心）、`cn.etetet.unit`（单位系统）、`cn.etetet.login`（登录）

**包的层级关系**（高层依赖低层，详见 `Packages/cn.etetet.harness/AGENTS.md`）：

| 层级 | 代表包 | 作用 |
|------|--------|------|
| 第1层（最底层） | `core` / `excel` / `proto` / `loader` | 框架基础 |
| 第2层 | `unit` / `behaviortree` / `http` / `startconfig` / `console` / `yooassets` / `yiui*` | 基础功能 |
| 第3层 | `numeric` / `move` / `nav2d` / `netinner` / `router` / `item` | 中层系统 |
| 第4层 | `actorlocation` / `aoi` / `map` | 高层系统 |
| 第5层（最顶层） | `wow` / `btnode` / `test` / `robot` / `login` / `mapplay` / `statesync` | 业务入口 |

### 2. 主包（MainPackage）概念

**主包是游戏的入口包**，决定了：
- 游戏启动时加载哪个场景
- 游戏的入口逻辑代码在哪
- 整个项目依赖哪些包（主包的依赖会被 Unity Package Manager 自动递归引入）

例如：
- 主包 = `cn.etetet.statesync`：状态同步 Demo，入口是状态同步场景（包含帧同步/状态同步玩法）
- 主包 = `cn.etetet.login`：登录 Demo，入口是登录场景

主包名格式：`cn.etetet.{SceneName 小写}`，例如 `SceneName=StateSync` → 主包 `cn.etetet.statesync`。

### 3. 四层代码架构

ET 把每个包内的代码分为四层，每层职责不同：

| 层级 | 目录 | 职责 | 是否热更新 | 是否依赖 Unity |
|------|------|------|-----------|---------------|
| **Model** | `Scripts/Model/` | 数据定义（Entity、Component） | 否 | 客户端：是；服务端：否 |
| **ModelView** | `Scripts/ModelView/` | 视图相关的数据定义 | 否 | 是 |
| **Hotfix** | `Scripts/Hotfix/` | 业务逻辑（System、Handler） | **是** | 客户端：是；服务端：否 |
| **HotfixView** | `Scripts/HotfixView/` | 视图相关逻辑（UISystem） | **是** | 是 |

关键理解：
- **Model 层**：定义数据结构（Entity 派生类、Component 派生类），编译期生成 dll，不参与热更新
- **Hotfix 层**：业务逻辑代码（System、EventHandler），运行时通过 HybridCLR 热更新（无需重编客户端）
- **ModelView / HotfixView**：与 Unity 交互的视图层，只在客户端存在（服务端不依赖 UnityEngine）

### 4. 三端代码分离（CodeMode）

ET 支持客户端、服务端分离开发，每个包的代码按端划分：

```
Scripts/Model/Share/      ← 共享代码（客户端+服务端都用，如战斗公式）
Scripts/Model/Client/     ← 仅客户端代码（如 UI 数据）
Scripts/Model/Server/     ← 仅服务端代码（如数据库存储字段）
Scripts/Hotfix/Share/     ← 同理
Scripts/Hotfix/Client/
Scripts/Hotfix/Server/
```

**CodeMode 决定编译时生成哪些代码：**
- `Client`：只生成 `Share` + `Client` 目录代码（用于打包客户端）
- `Server`：只生成 `Share` + `Server` 目录代码（用于部署服务端）
- `ClientServer`：生成所有目录代码（用于本地双端联调开发）

### 5. asmdef / asmref 程序集管理（理解 CodeMode 切换的关键）

Unity 通过 **asmdef** 文件定义程序集，**asmref** 文件让某个目录"归属"到指定的 asmdef 程序集。

ET 框架的核心程序集（asmdef 定义）：
- `ET.Model` → Model 层代码汇总
- `ET.ModelView` → ModelView 层代码汇总
- `ET.Hotfix` → Hotfix 层代码汇总
- `ET.HotfixView` → HotfixView 层代码汇总

每个包的 `Scripts/Model/Client/AssemblyReference.asmref` 文件内容决定该目录归属：
- `ClientServer` 模式下：`{ "reference": "ET.Model" }` → Client 目录代码进入 `ET.Model` 程序集
- `Server` 模式下：Client 目录的 asmref 指向空 → Client 代码不参与编译（因为服务端不需要客户端代码）

**CodeMode 切换的本质就是修改这些 asmref 文件的内容**，由 ET.CodeMode 工具完成。

### 6. 全局配置 GlobalConfig.asset

`Packages/com.etetet.init/Resources/GlobalConfig.asset` 是 ET 的全局配置文件（Unity ScriptableObject 序列化为 YAML-like 文本），存储：
- `SceneName`：默认场景名（用于推导主包）
- `CodeMode`：默认代码模式

这是 Unity 中可配置的（通过 Inspector 修改），脚本会从这里读取默认值，让用户不必每次都传命令行参数。

### 7. MainPackage.txt 的作用

`MainPackage.txt` 是 ET 工具链的关键文件，位于项目根目录。内容格式：

```
cn.etetet.statesync          ← 第一行：主包名
cn.etetet.core                ← 后续每行：主包的直接依赖
cn.etetet.config
cn.etetet.proto
...
```

它的作用：
- **代码裁剪**：ET.CodeMode 工具只对 `MainPackage.txt` 列出的包生成 asmref 文件，没列出的包不参与编译
- **csproj 生成**：影响哪些代码会被包含到 `ET.sln` 解决方案
- **构建系统**：Unity 编译时根据 asmref 决定代码归属哪个程序集

### 8. ET.sln 主解决方案

`ET.sln` 是 Visual Studio / Rider 解决方案文件，包含所有需要编译的 csproj 项目。

**为什么需要硬链接到根目录：**
- 主包目录（如 `Packages/cn.etetet.statesync/ET.sln`）有原始 sln 文件
- 但 Rider/VS 通常从项目根目录打开解决方案
- 通过硬链接，根目录的 `ET.sln` 和主包目录下的 `ET.sln` 是**同一份文件**，修改一处两处同步

---

## 四、核心流程详解

脚本执行流程分 7 个步骤，下面逐步详解每一步**做什么、为什么、在 ET 框架中起什么作用**。

### 步骤 1：解析初始化参数

#### 涉及代码

```powershell
function Resolve-InitializationOptions {
    $globalConfigPath = Resolve-ProjectPath 'Packages/com.etetet.init/Resources/GlobalConfig.asset'

    # 1. 解析 SceneName
    if ([string]::IsNullOrWhiteSpace($script:SceneName)) {
        $script:SceneName = Get-GlobalConfigValue -GlobalConfigPath $globalConfigPath -Name 'SceneName'
    }

    # 2. 解析 MainPackage
    if ([string]::IsNullOrWhiteSpace($script:MainPackage)) {
        if ([string]::IsNullOrWhiteSpace($script:SceneName)) {
            throw '未指定 SceneName 或 MainPackage，且 GlobalConfig.asset 中没有 SceneName'
        }
        $script:MainPackage = "cn.etetet.$($script:SceneName.ToLowerInvariant())"
    }

    # 3. 解析 CodeMode
    if ([string]::IsNullOrWhiteSpace($script:CodeMode)) {
        $rawCodeMode = Get-GlobalConfigValue -GlobalConfigPath $globalConfigPath -Name 'CodeMode'
        $script:CodeMode = Convert-CodeModeValue -Value $rawCodeMode
    }

    # 4. CodeMode 兜底默认值
    if ([string]::IsNullOrWhiteSpace($script:CodeMode)) {
        $script:CodeMode = 'ClientServer'
    }
}
```

#### 逐行分析

| 代码 | 作用 |
|------|------|
| `$globalConfigPath = Resolve-ProjectPath 'Packages/com.etetet.init/Resources/GlobalConfig.asset'` | 定位 ET 全局配置文件，这是 Unity 中可配置的默认值来源 |
| `if ([string]::IsNullOrWhiteSpace($script:SceneName))` | 检查命令行是否传了 `-SceneName`，没传则进入分支从配置文件读 |
| `$script:SceneName = Get-GlobalConfigValue ... 'SceneName'` | 从 `GlobalConfig.asset` 读取 `SceneName` 字段（按行正则匹配 `SceneName: xxx`） |
| `if ([string]::IsNullOrWhiteSpace($script:MainPackage))` | 检查命令行是否传了 `-MainPackage`，没传则由 SceneName 推导 |
| `$script:MainPackage = "cn.etetet.$($script:SceneName.ToLowerInvariant())"` | 拼接主包名，例如 `SceneName=StateSync` → `cn.etetet.statesync`。`ToLowerInvariant()` 转小写避免区域文化差异 |
| `$rawCodeMode = Get-GlobalConfigValue ... 'CodeMode'` | 从 `GlobalConfig.asset` 读取 `CodeMode` 字段 |
| `$script:CodeMode = Convert-CodeModeValue -Value $rawCodeMode` | 规范化 CodeMode（GlobalConfig 中可能是数字 1/2/3） |
| `$script:CodeMode = 'ClientServer'` | 兜底默认值：如果都没配置，默认双端模式（本地开发最常用） |

#### 参数解析优先级

```
命令行参数  >  GlobalConfig.asset  >  默认值
```

#### 在 ET 框架中的作用

这一步确定了三个决定项目编译方向的核心参数：

1. **SceneName**：决定游戏入口场景。例如 `StateSync` 表示启动状态同步 Demo 场景。
2. **MainPackage**：决定项目入口包。所有代码裁剪、csproj 生成都以主包为根。
3. **CodeMode**：决定编译哪些端的代码。直接影响 `ET.Model` / `ET.Hotfix` 程序集中包含哪些目录的代码。

如果这三个参数没确定，后续步骤无法进行（不知道生成谁的 asmref，不知道编译哪端代码）。

---

### 步骤 2：主包初始化（生成 MainPackage.txt + 链接 ET.sln）

#### 涉及代码

```powershell
function Initialize-MainPackage {
    param([string]$PackageName)

    $packageInfo = Resolve-PackageInfo -PackageName $PackageName
    $dependencies = Get-DirectEtDependencies -PackageJson $packageInfo.PackageJson -MainPackageName $packageInfo.Name
    Write-Step "生成主包: $($packageInfo.Name)，直接依赖数量: $($dependencies.Count)"
    Write-MainPackageFile -MainPackageName $packageInfo.Name -Dependencies $dependencies
    Link-MainPackageSolution -PackageDirectory $packageInfo.Directory
}
```

主包初始化包含三个子动作：

#### 子动作 2.1：解析主包 package.json

`Resolve-PackageInfo` 函数根据包名定位目录并读取 `package.json`：

```powershell
# 拼接目标包目录路径
$packageDir = Join-Path $packagesDir $PackageName
# 大小写不一致时在 Packages 下查找匹配目录
if (!(Test-Path -LiteralPath $packageDir -PathType Container)) {
    $match = Get-ChildItem -LiteralPath $packagesDir -Directory -Filter 'cn.etetet.*' |
        Where-Object { $_.Name -ieq $PackageName } |
        Select-Object -First 1
    ...
}
```

以 `cn.etetet.statesync` 为例，读取的 `package.json` 包含 41 个直接依赖（如 `cn.etetet.core`、`cn.etetet.unit`、`cn.etetet.login` 等）。

#### 子动作 2.2：生成 MainPackage.txt

`Write-MainPackageFile` 函数把主包名 + 依赖列表写入 `MainPackage.txt`：

```powershell
$mainPackagePath = Resolve-ProjectPath 'MainPackage.txt'
$lines = @($MainPackageName) + @($Dependencies)
Set-Content -LiteralPath $mainPackagePath -Value $lines -Encoding utf8
```

产出文件内容示例：

```
cn.etetet.statesync
cn.etetet.core
cn.etetet.config
cn.etetet.proto
cn.etetet.unit
... (共 42 行，第 1 行主包名 + 41 行依赖)
```

#### 子动作 2.3：硬链接主包 ET.sln 到根目录

`Link-MainPackageSolution` 函数使用"临时硬链接 + Move-Item"的原子替换方式：

```powershell
$sourceSolutionPath = Join-Path $PackageDirectory 'ET.sln'  # 主包目录下的 ET.sln
$rootSolutionPath = Resolve-ProjectPath 'ET.sln'             # 项目根目录的 ET.sln
$tempLinkPath = Resolve-ProjectPath 'ET.sln.mainpackage_link_tmp'

# 创建临时硬链接
New-Item -ItemType HardLink -Path $tempLinkPath -Target $sourceSolutionPath | Out-Null
# 删除根目录旧 ET.sln
Remove-Item -LiteralPath $rootSolutionPath -Force -ErrorAction SilentlyContinue
# 原子替换：重命名临时链接为正式文件名
Move-Item -LiteralPath $tempLinkPath -Destination $rootSolutionPath -Force
```

#### 在 ET 框架中的作用

这是 ET 项目能正确编译的**根本前提**：

1. **MainPackage.txt 是 ET 工具链的"裁剪清单"**：
   - 后续 ET.CodeMode 工具会读取这个文件，**只对列表中的包生成 asmref 文件**
   - 没在列表中的包不会进入 `ET.Model` / `ET.Hotfix` 程序集，相当于被排除在编译之外
   - 这就是 ET 框架如何实现"一个项目支持多种玩法"的机制：换主包 = 换游戏入口

2. **ET.sln 是唯一编译入口**：
   - 根据 ET 框架规范，**项目只有一个编译命令：`dotnet build ET.sln`**
   - 主包目录下有原始的 `ET.sln`，但 Rider/VS 习惯从根目录打开
   - 硬链接保证两处是同一份文件，避免编辑器从不同位置打开导致分歧

3. **为什么用硬链接而非复制**：
   - 复制会产生两份独立文件，修改一处另一处不变，容易冲突
   - 硬链接是文件系统的多个入口指向同一份数据，修改任何一处都同步
   - 适合"同一份文件需要在多个位置访问"的场景

---

### 步骤 3：-GenerateMainPackageOnly 短路检查

#### 涉及代码

```powershell
if ($GenerateMainPackageOnly) {
    Write-Step '已按参数仅生成主包，跳过后续构建'
    exit 0
}
```

#### 在 ET 框架中的作用

这是一个**调试用短路**：如果用户只想验证主包配置是否正确（package.json 依赖是否完整、MainPackage.txt 是否生成成功），不需要执行后续耗时的编译步骤，可以加 `-GenerateMainPackageOnly` 开关快速验证。

典型用法：
- 修改了主包的 `package.json` 依赖后，先 `-GenerateMainPackageOnly` 验证依赖链
- 验证无误后再去掉开关执行完整初始化

---

### 步骤 4：环境检查（Assert-Environment）

#### 涉及代码

```powershell
function Assert-Environment {
    Write-Step '检查初始化环境'

    # 1. 检查目录
    Assert-DirectoryExists -Path $ProjectRoot -Description '项目根目录'
    Assert-DirectoryExists -Path (Resolve-ProjectPath 'Packages') -Description 'Packages 目录'

    # 2. 定位 Git
    $script:ResolvedGitExecutable = Resolve-GitExecutable

    # 3. 检查 .NET SDK 版本 >= 10
    $dotnetInfo = & dotnet --list-sdks
    $hasCompatibleSdk = $false
    foreach ($line in $dotnetInfo) {
        if ($line -match '^(\d+)\.') {
            $major = [int]$Matches[1]
            if ($major -ge 10) { $hasCompatibleSdk = $true }
        }
    }
    if (!$hasCompatibleSdk) {
        throw ".NET SDK 版本过低，需要 10 或更高版本。"
    }

    # 4. 检查关键文件
    Assert-FileExists -Path (Resolve-ProjectPath 'Packages/cn.etetet.yiuiluban/DontNet~/luban/src/Luban.sln') -Description 'Luban.sln'
    Assert-FileExists -Path (Resolve-ProjectPath 'Packages/com.etetet.init/DotNet~/ET.CodeMode.csproj') -Description 'ET.CodeMode.csproj'
    Assert-FileExists -Path (Resolve-ProjectPath 'Packages/cn.etetet.sourcegenerator/DotNet~/ET.SourceGenerator/ET.SourceGenerator.csproj') -Description 'ET.SourceGenerator.csproj'
    Assert-FileExists -Path (Resolve-ProjectPath 'ET.sln') -Description 'ET.sln'
}
```

#### 逐项检查内容

| 检查项 | 期望状态 | 在 ET 中的作用 |
|--------|----------|----------------|
| 项目根目录 | 必须存在 | ET 项目根，所有相对路径基准 |
| `Packages/` 目录 | 必须存在 | Unity Package 根目录，存放所有 `cn.etetet.*` 包 |
| Git 可执行文件 | 必须可用 | 后续还原 `.meta` 文件需要 git |
| .NET SDK 版本 | ≥ 10 | ET 框架基于 .NET 10，工具链需要新 SDK 特性 |
| `Luban.sln` | 必须存在 | Luban 配置表工具的解决方案，下一步要编译 |
| `ET.CodeMode.csproj` | 必须存在 | CodeMode 切换工具项目，下一步要编译 |
| `ET.SourceGenerator.csproj` | 必须存在 | ET 源生成器项目，下一步要编译 |
| `ET.sln` | 必须存在 | 主解决方案，由步骤 2 链接到根目录 |

#### 在 ET 框架中的作用

这一步是**"防呆检查"**：在执行耗时编译前先验证所有依赖就位，避免编译到一半才报错。

特别说明几个关键依赖：

1. **.NET SDK ≥ 10**：ET10 框架（"昭君"）基于 .NET 10，使用了 C# 13+ 的新特性（如 `field` 关键字、`partial` 属性等），低版本 SDK 无法编译。

2. **`com.etetet.init` 包**：这是 ET 的初始化包，包含：
   - `Runtime/GlobalConfig.asset`：全局配置
   - `DotNet~/ET.CodeMode.csproj`：CodeMode 切换工具（C# 控制台程序）
   - 脚本中的 `CodeModeChangeHelper.cs` 负责实际修改 asmref 文件

3. **`cn.etetet.yiuiluban` 包**：YIUI 集成的 Luban 配置表工具，导出 Excel 配置为 C# 代码。

4. **`cn.etetet.sourcegenerator` 包**：ET 的 C# 源生成器（Roslyn Analyzer），编译期自动生成 ET 框架样板代码（如 `Entity` 的事件注册、`Component` 的 `ComponentOf` 标记处理等）。

---

### 步骤 5：编译 Luban + ET.CodeMode + ET.SourceGenerator

#### 涉及代码

```powershell
function Invoke-InitializationBuilds {
    $solutionDir = $ProjectRoot
    $separator = [string][System.IO.Path]::DirectorySeparatorChar
    if (!$solutionDir.EndsWith($separator, [System.StringComparison]::Ordinal)) {
        $solutionDir = "$solutionDir$separator"
    }

    # 阶段 1：编译 Luban
    if (!$SkipLubanBuild) {
        Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @(
            'build',
            (Resolve-ProjectPath 'Packages/cn.etetet.yiuiluban/DontNet~/luban/src/Luban.sln'),
            '-p:WarningsNotAsErrors=NU1903'
        ) -Description '编译 Luban'
    }

    # 阶段 2：编译 ET 工具
    if (!$SkipToolBuild) {
        Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @(
            'build',
            (Resolve-ProjectPath 'Packages/com.etetet.init/DotNet~/ET.CodeMode.csproj'),
            "-p:SolutionDir=$solutionDir"
        ) -Description '编译 ET.CodeMode'

        Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @(
            'build',
            (Resolve-ProjectPath 'Packages/cn.etetet.sourcegenerator/DotNet~/ET.SourceGenerator/ET.SourceGenerator.csproj'),
            "-p:SolutionDir=$solutionDir"
        ) -Description '编译 ET.SourceGenerator'
    }

    # 阶段 3：执行 ET.CodeMode.dll 切换 CodeMode
    if (!$SkipCodeMode) {
        Assert-FileExists -Path (Resolve-ProjectPath 'Bin/ET.CodeMode.dll') -Description 'Bin/ET.CodeMode.dll'
        Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @(
            (Resolve-ProjectPath 'Bin/ET.CodeMode.dll'),
            "--CodeMode=$CodeMode"
        ) -Description "执行 ET.CodeMode: $CodeMode"
    }

    # 阶段 4：可选编译 ET.sln
    if ($BuildSolution -and !$SkipFullBuild) {
        Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @(
            'build',
            (Resolve-ProjectPath 'ET.sln')
        ) -Description '编译 ET.sln'
    }
}
```

#### 阶段 5.1：编译 Luban（配置表工具）

**Luban 是什么：**
Luban 是一款开源的游戏配置表导出工具，ET 框架深度集成。它读取 Excel 配置表（.xlsx），导出：
- C# 配置代码类（定义表结构）
- C# 数据代码（运行时读取的二进制/JSON 配置数据）
- 多语言分支支持

**在 ET 中的作用：**
- 游戏中的所有静态配置（角色属性、技能参数、关卡数据、道具配置等）都用 Luban 管理
- 设计师在 Excel 中改数值，开发者执行 Luban 导出生成 C# 代码
- 运行时游戏代码通过 Luban 生成的 API 读取配置

**为什么需要先编译 Luban：**
Luban 本身是一个独立的 .NET 工具，需要先编译出 `Luban.exe`（或 dll），后续 ET 的配置导出流程才能调用它。

**`-p:WarningsNotAsErrors=NU1903` 的含义：**
NU1903 是 NuGet 包版本警告（某个依赖包版本较旧），Luban 默认把警告视为错误会阻塞编译，这里降级为普通警告放行。

#### 阶段 5.2：编译 ET.CodeMode（代码模式切换工具）

**ET.CodeMode 是什么：**
ET.CodeMode 是一个 C# 控制台工具，源码在 `Packages/com.etetet.init/DotNet~/`，核心是 `CodeModeChangeHelper.cs`。

**在 ET 中的作用：**
读取 `MainPackage.txt` 列出的所有包，根据 CodeMode（Client/Server/ClientServer）修改这些包内的 `AssemblyReference.asmref` 文件，决定哪些目录的代码参与编译。

**工作原理示例：**
假设 `cn.etetet.unit/Scripts/Model/Client/AssemblyReference.asmref` 文件：

| CodeMode | 文件内容 | 效果 |
|----------|----------|------|
| `ClientServer` | `{ "reference": "ET.Model" }` | Client 目录代码进入 ET.Model 程序集，参与编译 |
| `Server` | （指向空或忽略） | Client 目录代码不参与编译（服务端不需要客户端代码） |
| `Client` | `{ "reference": "ET.Model" }` | Client 目录代码参与编译（客户端需要） |

**`-p:SolutionDir=$solutionDir` 的含义：**
传入解决方案目录（带尾部 `\`），影响 csproj 内部相对路径的解析。ET 工具项目依赖这个变量定位输出目录。

#### 阶段 5.3：编译 ET.SourceGenerator（源生成器）

**ET.SourceGenerator 是什么：**
ET 框架基于 C# Roslyn Source Generator 实现的源生成器（编译期代码生成）。源码在 `Packages/cn.etetet.sourcegenerator/`。

**在 ET 中的作用：**
编译期自动生成 ET 框架所需的样板代码，避免手写：
- `Entity` 派生类的事件注册代码
- `Component` 的 `ComponentOf` / `ChildOf` 标记处理
- `System` 类的 `Run` 方法注册
- RPC 消息的 Handler 注册

**为什么需要 SourceGenerator：**
ET 框架使用 ECS（Entity-Component-System）架构，每个 Entity/Component/System 都需要注册到 `EventSystem`。手写注册代码容易遗漏，源生成器在编译期自动处理，确保不遗漏。

**输出文件：**
编译产出 `Packages/cn.etetet.sourcegenerator/ET.SourceGenerator.dll`，Unity/Roslyn 在编译 C# 代码时会自动加载这个 dll 作为分析器。

#### 阶段 5.4：执行 ET.CodeMode.dll 切换 CodeMode

**这一步是初始化流程的核心动作之一：**

```powershell
Assert-FileExists -Path (Resolve-ProjectPath 'Bin/ET.CodeMode.dll') -Description 'Bin/ET.CodeMode.dll'
Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @(
    (Resolve-ProjectPath 'Bin/ET.CodeMode.dll'),
    "--CodeMode=$CodeMode"
) -Description "执行 ET.CodeMode: $CodeMode"
```

- `Bin/ET.CodeMode.dll`：上一步编译产出的工具 dll
- `--CodeMode=$CodeMode`：传入命令行参数指定要切换到的模式
- `dotnet xxx.dll`：通过 .NET 运行时执行 dll

**执行效果：**
ET.CodeMode 工具会遍历 `MainPackage.txt` 列出的所有包，修改每个包内的 asmref 文件，使其符合当前 CodeMode 的预期。

**为什么这一步至关重要：**
- 如果 asmref 文件不正确，Unity 编译时无法正确分组代码到 `ET.Model` / `ET.Hotfix` 等程序集
- 程序集分组错误会导致类型找不到、跨层引用失败等编译错误
- 这就是为什么 ET 项目改了 CodeMode 后必须重新跑初始化脚本

#### 阶段 5.5：（可选）编译整个 ET.sln

```powershell
if ($BuildSolution -and !$SkipFullBuild) {
    Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @(
        'build',
        (Resolve-ProjectPath 'ET.sln')
    ) -Description '编译 ET.sln'
}
```

**为什么要单独开关：**
- 默认不编译 `ET.sln`，因为这一步耗时长（几分钟到十几分钟）
- Unity 打开项目时会自动触发编译，所以命令行可以不重复编译
- 但 CI/CD 场景需要确认整个解决方案能编译通过，这时加 `-BuildSolution` 开关

---

### 步骤 6：还原 SourceGenerator 的 .meta 文件

#### 涉及代码

```powershell
function Restore-SourceGeneratorMeta {
    $metaRelativePath = 'Packages/cn.etetet.sourcegenerator/ET.SourceGenerator.dll.meta'
    $metaPath = Resolve-ProjectPath $metaRelativePath

    if (!(Test-Path -LiteralPath $metaPath -PathType Leaf)) {
        Write-Warning "未找到 SourceGenerator meta 文件，跳过 git 还原: $metaRelativePath"
        return
    }

    Invoke-CheckedCommand -FilePath $script:ResolvedGitExecutable -ArgumentList @(
        'restore',
        '--',
        $metaRelativePath
    ) -Description "还原 SourceGenerator meta 文件: $metaRelativePath"
}
```

#### 在 ET 框架中的作用

**Unity .meta 文件的本质：**
- Unity 中每个资源文件（包括 dll）都有对应的 `.meta` 文件
- `.meta` 文件包含一个 GUID（全局唯一标识符）
- 其他 Unity 资源通过 GUID 引用该资源
- **如果 GUID 变了，所有引用该资源的地方都会断裂**

**问题场景：**
- 步骤 5.3 编译 `ET.SourceGenerator.dll` 时，Unity 检测到 dll 变化，可能重新生成 `.meta` 文件
- 新生成的 `.meta` 文件 GUID 与仓库中的原始 GUID 不一致
- 导致项目中所有依赖 SourceGenerator 的地方引用断裂

**解决方法：**
- `git restore -- Packages/cn.etetet.sourcegenerator/ET.SourceGenerator.dll.meta`
- 把 `.meta` 文件还原为仓库中的原始版本
- 保持 GUID 稳定，避免 Unity 引用断裂

**`--` 的含义：**
Git 命令中 `--` 表示"后续参数都是文件路径而非选项"，避免路径以 `-` 开头时被误识别为 git 选项。

---

### 步骤 7：输出人工步骤提示

#### 涉及代码

```powershell
Write-Step '命令行初始化流程已完成'
Write-Host ''
Write-Host '仍需在 Unity/Rider 中确认的人工步骤：'
Write-Host '- Unity 版本使用 2022.3.62'
Write-Host '- 已安装 DOTween 和 Odin 插件'
Write-Host '- 已安装 IL2CPP 模块'
Write-Host '- Unity External ScriptEditor 选择 Rider，并勾选 Generate .csproj files for 的前两个选项'
Write-Host '- 打开 Packages/ET.StateSync/Scenes/Init 场景后进入 Play'
```

#### 每项说明

| 提示项 | 在 ET 中的作用 |
|--------|----------------|
| Unity 2022.3.62 | ET10 框架要求的最小 Unity 版本，低于此版本可能有 API 缺失 |
| DOTween 插件 | 缓动动画库，YIUI 框架依赖它做 UI 动画 |
| Odin 插件 | Sirenix Odin Inspector，强大的编辑器扩展库，YIUI 大量使用它做 Inspector 增强 |
| IL2CPP 模块 | Unity 的 IL2CPP 后端，HybridCLR 热更新方案依赖它（IL2CPP 比 Mono 性能更好，且支持 AOT+解释器混合模式） |
| Rider + csproj 选项 | 推荐的 IDE，勾选生成 csproj 让 Rider 能正确识别 ET 的程序集结构 |
| Init 场景 | ET 的入口场景，位于主包内（`Packages/cn.etetet.statesync/Scenes/Init.unity`），包含 `GlobalConfig` 和启动逻辑 |

**为什么脚本不能自动完成这些：**
这些是 Unity Editor 的图形化配置（安装插件、选择 IDE、打开场景），命令行无法操作，必须人工在 Unity 中完成。

---

## 五、关键设计点

### 1. 错误处理策略

- `$ErrorActionPreference = 'Stop'`：任何非终止性错误都立即抛异常终止脚本
- 所有外部命令通过 `Invoke-CheckedCommand` 调用，检查 `$LASTEXITCODE`
- `Push-Location` / `Pop-Location` + `try/finally`：保证异常退出时恢复原工作目录

### 2. 硬链接 ET.sln 的原子替换

采用"临时硬链接 + Move-Item"的原子替换方式，避免直接覆盖正在使用的文件：

```powershell
New-Item -ItemType HardLink -Path $tempLinkPath -Target $sourceSolutionPath
Remove-Item -LiteralPath $rootSolutionPath -Force
Move-Item -LiteralPath $tempLinkPath -Destination $rootSolutionPath -Force
```

### 3. 参数三层优先级

```
命令行参数  >  GlobalConfig.asset  >  默认值
```

这种设计兼顾两种使用场景：
- **人工使用**：在 Unity 中配置 `GlobalConfig.asset`，命令行无需传参
- **CI/CD 自动化**：通过命令行参数覆盖配置，便于不同构建配置切换

### 4. .NET SDK 版本要求

要求 .NET SDK 主版本号 **≥ 10**。脚本通过 `dotnet --list-sdks` 获取已安装列表，正则匹配 `^(\d+)\.` 提取主版本号判断。

### 5. Luban 编译的特殊处理

Luban 编译时传入 `-p:WarningsNotAsErrors=NU1903`，把 NuGet 包版本警告降级为普通警告，避免阻塞编译。

---

## 六、常用调用方式

### 1. 完整初始化（最常用）

```powershell
pwsh ./ET-master/Scripts/Initialize-Project.ps1
```

### 2. 仅生成主包文件，不做编译（快速验证主包配置）

```powershell
pwsh ./ET-master/Scripts/Initialize-Project.ps1 -GenerateMainPackageOnly
```

### 3. 指定 CodeMode 为客户端

```powershell
pwsh ./ET-master/Scripts/Initialize-Project.ps1 -CodeMode Client
```

### 4. 指定场景名（会推导主包为 `cn.etetet.login`）

```powershell
pwsh ./ET-master/Scripts/Initialize-Project.ps1 -SceneName Login
```

### 5. 完整流程 + 编译整个 ET.sln

```powershell
pwsh ./ET-master/Scripts/Initialize-Project.ps1 -BuildSolution
```

### 6. 跳过某些步骤（调试用）

```powershell
pwsh ./ET-master/Scripts/Initialize-Project.ps1 -SkipLubanBuild -SkipToolBuild
```

### 7. CI/CD 场景：指定 git 完整路径

```powershell
pwsh ./ET-master/Scripts/Initialize-Project.ps1 -GitExecutable "C:\Program Files\Git\bin\git.exe"
```

> **注意**：根据 ET 框架 `AGENTS.md` 规范，本项目所有命令必须使用 `pwsh`（PowerShell 7），不要使用 Windows 自带的 `powershell.exe`。

---

## 七、产出文件

脚本执行后会产出/修改以下文件：

| 文件 | 位置 | 说明 |
|------|------|------|
| `MainPackage.txt` | `ET-master/` | 主包名 + 直接依赖列表（每行一个），CodeMode 工具的裁剪清单 |
| `ET.sln` | `ET-master/` | 硬链接自主包目录，根目录与包内同步 |
| `Bin/ET.CodeMode.dll` | `ET-master/Bin/` | ET.CodeMode 工具编译产物 |
| `Bin/ET.SourceGenerator.dll` | `ET-master/Bin/` | ET.SourceGenerator 编译产物 |
| `Packages/cn.etetet.sourcegenerator/ET.SourceGenerator.dll.meta` | 包内 | `git restore` 还原为原始版本，保持 GUID 稳定 |
| 各包的 `AssemblyReference.asmref` | 各包内 | ET.CodeMode 工具修改，决定代码归属哪个程序集 |

---

## 八、初始化后仍需人工确认的事项

脚本执行完成后，控制台会输出以下提示，需要在 Unity/Rider 中手动确认：

- Unity 版本使用 **2022.3.62**
- 已安装 **DOTween** 和 **Odin** 插件
- 已安装 **IL2CPP** 模块
- Unity External ScriptEditor 选择 **Rider**，并勾选 `Generate .csproj files for` 的前两个选项
- 打开 `Packages/cn.etetet.statesync/Scenes/Init` 场景后进入 Play

---

## 九、常见问题排查

### 1. 报错"未检测到 Git"

- 未安装 Git，请安装后重试
- 或通过 `-GitExecutable` 传入 git 完整路径

### 2. 报错".NET SDK 版本过低，需要 10 或更高版本"

- 通过 `dotnet --list-sdks` 查看已安装版本
- 安装 .NET SDK 10 或更高版本：<https://dotnet.microsoft.com/download>

### 3. 报错"未找到主包目录: ..."

- 检查 `Packages/` 目录下是否存在 `cn.etetet.{SceneName 小写}` 目录
- 或直接通过 `-MainPackage` 传入完整包名

### 4. 报错"未指定 SceneName 或 MainPackage，且 GlobalConfig.asset 中没有 SceneName"

- 命令行传入 `-SceneName` 或 `-MainPackage`
- 或在 `Packages/com.etetet.init/Resources/GlobalConfig.asset` 中配置 `SceneName` 字段

### 5. 报错"非法主包名: ..."

- 主包名必须以 `cn.etetet.` 开头（ET 框架包命名规范）

### 6. 报错"链接主包 ET.sln 失败"

- 检查主包目录下是否存在 `ET.sln`
- 检查文件系统是否支持硬链接（NTFS 支持，FAT32 不支持）
- 检查根目录是否有权限写入

### 7. 编译失败"NU1903 警告视为错误"

- Luban 编译时已通过 `-p:WarningsNotAsErrors=NU1903` 处理
- 如果仍报错，检查 Luban 依赖的 NuGet 包版本

### 8. Unity 中代码大量报错"找不到类型 Entity / ComponentOf"

- 检查是否执行过初始化脚本（asmref 文件未生成）
- 检查 `MainPackage.txt` 是否包含当前包
- 重新执行 `pwsh ./ET-master/Scripts/Initialize-Project.ps1` 不带任何跳过开关

---

## 十、ET 框架核心概念问答（新手FAQ）

本章整理新手学习 ET 框架时常问的核心概念问题，帮助理解主包、场景、同步方式等关键概念。

### Q1：使用 ET 框架开发，第一步要创建主包？还是在 ET 已有的主包上开发？

**两种方式都可以，看你的阶段。**

ET 框架的设计哲学是"**主包 = 游戏入口**"，所以可以创建新主包，也可以基于 ET 提供的 Demo 主包扩展。

#### 三种常见路径对比

**路径 A：学习阶段 → 直接在 ET Demo 主包上改**

**适用场景**：刚开始学 ET、做实验、写 Demo

ET 官方提供现成的可运行 Demo 主包（见 Q3）。

**做法：**
- 不创建新主包，直接在 `Packages/cn.etetet.statesync/Scripts/` 下的 Model / Hotfix / ModelView / HotfixView 目录添加业务代码
- 修改入口场景 `Packages/cn.etetet.statesync/Scenes/Init.unity`
- 你之前的"角色控制模块"就是这么做的（在 CodeMode 项目里写 PlayerController 等）

**优点**：能跑、依赖完整、不需要懂包制作
**缺点**：混入大量 Demo 代码，不适合正式项目

---

**路径 B：正式项目 → 创建自己的主包**

**适用场景**：做正式商业游戏、需要长期维护的项目

ET 官方文档（`Book/8.1ET Package制作指南.md` 第 8 条）明确：

> 假如 package 是一个完整的可运行的 demo，需要包含 DotNet~ 目录，里面放好 Model 跟 Hotfix 工程，需要将 ET.sln 工程复制到包中

**为什么正式项目要创建自己的主包：**

1. **精简依赖**：`cn.etetet.statesync` 依赖 41 个包，但你的游戏可能不需要帧同步、AOI、行为树等，自己主包只声明真正需要的依赖
2. **代码隔离**：Demo 主包里有大量演示代码（YIUI Demo、状态同步示例），正式项目不需要这些
3. **版本管理**：自己的主包可以用独立的 git 仓库管理，与 ET 框架核心包解耦
4. **符合 ET 设计哲学**：ET 的"主包 + 依赖包"模式类似于"游戏入口 + 功能模块"，主包应该只属于你这个游戏

**创建主包的关键步骤（基于官方文档）：**

```
1. 在 Packages/ 下新建目录，命名 cn.etetet.{你的游戏名}
2. 创建 package.json，声明：
   - name: cn.etetet.{你的游戏名}
   - dependencies: 只列出真正需要的 cn.etetet.* 包
3. 创建 Scripts/ 目录结构：
   Scripts/Model/{Share,Client,Server}
   Scripts/ModelView/{Share,Client,Server}
   Scripts/Hotfix/{Share,Client,Server}
   Scripts/HotfixView/{Share,Client,Server}
4. 创建 DotNet~/ 目录：
   - 放置 Model、Hotfix 的 csproj 工程
   - 复制一份 ET.sln 到主包目录
5. 创建 packagegit.json，分配唯一 Id（参考其他包）
6. 创建顶层 Ignore.asmdef（让代码默认不生效，等 Init 后才生效）
7. 修改 GlobalConfig.asset 的 SceneName 为你的游戏名
8. 运行 pwsh ./Scripts/Initialize-Project.ps1 重新初始化
```

---

**路径 C：折中方案 → 复制 Demo 主包后改造**

**适用场景**：想快速启动正式项目，又不想从零搭建

**做法：**
1. 复制 `Packages/cn.etetet.statesync/` 为 `Packages/cn.etetet.mygame/`
2. 修改 `package.json`：改 name、删除不需要的 dependencies
3. 删除 Demo 相关的业务代码（保留入口场景和基础结构）
4. 修改 `packagegit.json` 的 Id（向熊猫申请或自己分配一个未使用的编号）
5. 修改 `GlobalConfig.asset` 的 SceneName
6. 重新跑初始化脚本

#### 针对新手的建议

**推荐：路径 A（在 Demo 主包上开发）**

理由：
1. 你刚开始学 ET，重点应该放在理解 ECS 架构、四层代码分层、CodeMode、热更新等概念
2. 创建主包涉及 package.json 依赖管理、packagegit.json ID 分配、DotNet~ 工程搭建等，对新手是额外负担
3. ET 的 Demo 主包（statesync）已经把登录、单位管理、移动、AOI 等都跑通了，可以直接在这些系统上做实验

**等你熟悉后再考虑：路径 C（复制改造）或路径 B（从零创建）**

#### 决策树

```
是否要做正式商业游戏？
│
├── 否（学习/实验/原型）
│   └── 路径 A：在 cn.etetet.statesync 上直接写业务代码
│
└── 是（正式项目）
    │
    ├── 项目较小、想快速启动
    │   └── 路径 C：复制 statesync 改造
    │
    └── 项目较大、需要长期维护
        └── 路径 B：创建自己的主包
```

---

### Q2：一个主包就是一个场景吗？MainPackage.txt 是一个场景一份吗？其他主包的 MainPackage.txt 在哪里？

**这三个问题都涉及 ET 框架的核心概念，逐一解答。**

#### 1. 一个主包就是一个场景吗？

**不是。** 主包 ≠ 场景，主包 ⊃ 场景。

**主包是"游戏入口包"**，它包含的内容远不止一个场景：

```
Packages/cn.etetet.statesync/        ← 主包目录
├── package.json                     ← 包清单（声明依赖）
├── packagegit.json                  ← 包 git 元信息
├── Scripts/                         ← 业务代码
│   ├── Model/{Share,Client,Server}
│   ├── ModelView/
│   ├── Hotfix/
│   └── HotfixView/
├── Scenes/                          ← 场景集合（可以有多个）
│   ├── Init.unity                   ← 启动场景（入口）
│   ├── Login.unity                  ← 登录场景
│   └── Map.unity                    ← 地图场景
├── Proto/                           ← 消息协议定义
├── DotNet~/                         ← 服务端 csproj 工程
│   ├── Model/
│   ├── Hotfix/
│   └── ET.sln                       ← 主包专属的解决方案
└── Runtime/                         ← AOT 代码（非热更）
```

**关键区分：**
- **主包**：游戏的"启动配置 + 入口代码 + 入口场景 + 服务端工程"的组合
- **入口场景**（Init.unity）：主包中的一个场景文件，作为游戏启动时第一个加载的场景
- 一个主包通常有 1 个启动场景，但可以包含多个其他场景

#### 2. MainPackage.txt 是一个场景一份吗？

**不是。`MainPackage.txt` 是项目级文件，整个项目唯一一份。**

它位于项目根目录：`ET-master/MainPackage.txt`

当前内容：
```
cn.etetet.statesync          ← 第1行：当前主包
cn.etetet.actorlocation      ← 第2行起：主包的直接依赖
cn.etetet.aoi
...（共42行）
```

**它的本质：**
> "当前这个项目要编译哪个主包及其依赖包"

整个项目同一时间只有**一个主包生效**，所以只需要一份 MainPackage.txt。

#### 3. 其他主包的 MainPackage.txt 在哪里？

**这是个伪命题——其他主包没有自己的 MainPackage.txt。**

每个主包的"依赖清单"存在它自己的 `package.json` 里，而不是 MainPackage.txt。

#### 文件职责对照表

| 文件 | 位置 | 数量 | 作用 |
|------|------|------|------|
| `package.json` | 每个包目录内 | **每个包一份** | 声明该包的名称、版本、依赖 |
| `MainPackage.txt` | 项目根目录 | **整个项目唯一一份** | 声明当前项目使用的主包 + 其依赖（扁平化列表） |
| `ET.sln` | 项目根目录（硬链接）+ 主包目录内 | **当前主包一份** | 解决方案文件，决定编译哪些 csproj |

#### 类比理解

把 `package.json` 想成"个人简历"（每个包都有），把 `MainPackage.txt` 想成"项目组名单"（项目级唯一）：

```
所有 cn.etetet.* 包的 package.json     →  每个包都介绍自己是谁、依赖谁
              ↓
  Initialize-Project.ps1 读取当前主包的 package.json
              ↓
  扁平化（主包名 + 直接依赖）写入 MainPackage.txt
              ↓
  MainPackage.txt  →  ET 工具链根据它决定编译哪些包
```

#### 切换主包的工作流程

如果想从 `cn.etetet.statesync` 切换到 `cn.etetet.lockstep`，MainPackage.txt 会怎样变化？

```powershell
pwsh ./ET-master/Scripts/Initialize-Project.ps1 -SceneName Lockstep
```

**脚本内部做了什么：**

1. 读取 `Packages/cn.etetet.lockstep/package.json`（新主包的依赖）
2. **覆盖** `ET-master/MainPackage.txt` 为新内容：
   ```
   cn.etetet.lockstep              ← 新主包
   cn.etetet.core                  ← lockstep 包的依赖（比 statesync 少很多）
   cn.etetet.proto
   cn.etetet.unit
   ...（共 29 行：1 行主包名 + 28 行依赖）
   ```
3. 重新链接 `ET.sln`（从 `Packages/cn.etetet.lockstep/ET.sln` 链接到根目录）
4. 重新生成 asmref 文件（只对 MainPackage.txt 列出的包生成）

**结果：** `MainPackage.txt` 永远只有一份，内容会随主包切换而被覆盖。

#### 视觉化总结

```
┌─────────────────────────────────────────────────────────────────┐
│  ET-master/                                                     │
│  ├── MainPackage.txt  ◄── 项目唯一，表示当前主包 + 依赖         │
│  ├── ET.sln           ◄── 项目唯一，硬链接自当前主包            │
│  └── Packages/                                                  │
│      ├── cn.etetet.statesync/                                   │
│      │   ├── package.json   ◄── statesync 包的依赖清单          │
│      │   ├── ET.sln         ◄── statesync 主包的解决方案        │
│      │   └── Scenes/Init.unity                                  │
│      ├── cn.etetet.lockstep/                                    │
│      │   ├── package.json   ◄── lockstep 包的依赖清单           │
│      │   └── ET.sln         ◄── lockstep 主包的解决方案         │
│      ├── cn.etetet.login/                                       │
│      │   └── package.json   ◄── login 是功能包（无 ET.sln）     │
│      └── ...（其他功能包，没有 ET.sln，不能作为主包）            │
└─────────────────────────────────────────────────────────────────┘
```

**主包 vs 功能包的区别：**
- **主包**：有 `ET.sln`、（通常）有 `Scenes/Init.unity`、package.json 依赖很多包 → 可作为游戏入口
- **功能包**（如 `cn.etetet.core`、`cn.etetet.unit`）：没有 ET.sln、没有入口场景 → 被主包依赖

#### 一句话总结

> **MainPackage.txt 是项目级唯一文件，表示"当前项目用哪个主包"。切换主包 = 覆盖 MainPackage.txt + 重链 ET.sln + 重生成 asmref。每个主包自己的依赖清单在它各自的 package.json 里。**

---

### Q3：ET 项目中有哪些主包？

通过实际查询（`Packages/cn.etetet.*/ET.sln`），ET 框架当前提供 **2 个主包**：

| 主包名 | 版本 | 描述 | 是否含 Init 场景 | 直接依赖数 |
|--------|------|------|------------------|-----------|
| `cn.etetet.statesync` | 4.0.0 | 状态同步 MMO Demo（WOW 风格） | 是 | 41 个 |
| `cn.etetet.lockstep` | 3.0.2 | 预测回滚的帧同步 Demo | 否（复用 login 包场景） | 28 个 |

#### 两个主包的详细对比

**`cn.etetet.statesync`（状态同步，推荐学习）：**
- 版本：4.0.0（最新）
- 描述：WOW 风格的 MMO Demo
- 包含 `Scenes/Init.unity` 启动场景
- 依赖 41 个包，包含完整的 MMO 功能：
  - `cn.etetet.move` + `cn.etetet.recast`：移动 + 寻路
  - `cn.etetet.aoi`：九宫格 AOI
  - `cn.etetet.numeric`：数值系统
  - `cn.etetet.spell`：技能系统
  - `cn.etetet.quest`：任务系统
  - `cn.etetet.item`：道具系统
  - `cn.etetet.equipment`：装备系统
  - `cn.etetet.map` + `cn.etetet.mapplay` + `cn.etetet.mapmanager`：地图系统
  - `cn.etetet.btnode` + `cn.etetet.behaviortree`：行为树 AI

**`cn.etetet.lockstep`（帧同步）：**
- 版本：3.0.2（较旧）
- 描述：预测回滚的帧同步 Demo
- **没有自带 Init 场景**，复用 `cn.etetet.login` 包的场景
- 依赖 28 个包，包含帧同步专用功能：
  - `cn.etetet.truesync`：确定性物理引擎（基于 FixedUpdate）
  - `cn.etetet.lsentity`：帧同步专用 Entity 系统
- 不含状态同步特有的 move/recast/aoi/spell 等

#### 如何自行查询主包

简单方法：找 `Packages/` 下哪些包**同时包含** `ET.sln`（必备）和 `Scenes/Init.unity`（可选）：

```powershell
# 列出所有有 ET.sln 的包（即主包候选）
Get-ChildItem -Path 'ET-master\Packages' -Directory -Filter 'cn.etetet.*' |
    Where-Object { Test-Path (Join-Path $_.FullName 'ET.sln') } |
    Select-Object Name
```

或用 Glob 工具查找 `Packages/cn.etetet.*/ET.sln`。

#### 容易混淆的点

以下包**不是主包**（没有 ET.sln，只是功能包）：
- `cn.etetet.login`：登录功能包，被 statesync 和 lockstep 依赖
- `cn.etetet.test`：测试功能包
- `cn.etetet.robot`：机器人测试包
- 其他所有 `cn.etetet.*` 包：都是功能包，被主包依赖

---

### Q4：帧同步和状态同步可以一起在一个主包里吗？同一个场景里面呢？

**技术上可以放一个主包里（package.json 同时声明依赖），但实践上强烈不推荐。同一场景内更不建议混用。**

#### 技术可行性分析

**1. 版本兼容性问题（最致命）**

通过对比两个主包的 `package.json`：

| 依赖包 | lockstep 要求版本 | statesync 要求版本 | 是否兼容 |
|--------|-------------------|-------------------|----------|
| `cn.etetet.core` | 3.0.3 | 4.0.0 | **不兼容** |
| `cn.etetet.proto` | 3.0.2 | 4.0.0 | **不兼容** |
| `cn.etetet.config` | 3.0.0 | 4.0.0 | **不兼容** |
| `cn.etetet.login` | 3.0.0 | 4.0.0 | **不兼容** |
| `cn.etetet.loader` | 3.0.1 | 4.0.0 | **不兼容** |

**lockstep（3.0.2）和 statesync（4.0.0）属于不同大版本，核心包版本号冲突，无法同时依赖。** Unity Package Manager 会因为版本冲突报错。

**2. 同步系统架构完全不同**

| 维度 | 帧同步（lockstep） | 状态同步（statesync） |
|------|--------------------|-----------------------|
| 物理引擎 | `truesync`（确定性物理） | Unity 物理 / `move` |
| Entity 系统 | `lsentity`（帧同步专用） | `unit`（普通单位） |
| 输入处理 | 收集输入 → 每帧广播 → 各端模拟 | 客户端输入 → 服务端计算 → 广播状态 |
| 网络协议 | 输入帧包（小） | 状态快照（大） |
| 回滚机制 | 预测回滚 | 无（信任服务端） |
| 寻路 | 不需要（自己实现） | `recast`（NavMesh） |
| AOI | 不需要（小地图） | `aoi`（九宫格） |

#### 实践建议

**场景 1：一个主包里混用（不推荐）**
- 需要先把 lockstep 升级到 4.0.0（目前没有）
- 需要解决两套同步系统的代码冲突（truesync vs move，lsentity vs unit）
- 复杂度极高，ET 官方都不这样做（分成两个主包演示）

**场景 2：同一场景内混用（强烈不推荐）**
- 一个场景内的单位应该用同一种同步方式
- 混用会导致：
  - 帧同步单位的状态变化需要广播给状态同步单位
  - 状态同步单位的状态变化需要进入帧同步的输入队列
  - 回滚逻辑无法处理混合单位
- 实际项目中几乎没有先例

**场景 3：不同场景用不同同步方式（可行）**
- 登录场景 → 大厅场景（状态同步）→ 战斗场景（帧同步）
- 每个场景只加载对应同步系统的代码
- 这是 RTS/MOBA 游戏常见做法（大厅用状态同步，战斗用帧同步）
- 但需要主包同时依赖两套系统，回到场景 1 的版本兼容问题

#### 推荐做法

**根据游戏类型选择一种同步方式：**

| 游戏类型 | 推荐同步方式 | 推荐 Demo 主包 |
|----------|--------------|----------------|
| RTS / 格斗 / MOBA / 体育 | 帧同步 | `cn.etetet.lockstep` |
| MMO / ARPG / 开放世界 | 状态同步 | `cn.etetet.statesync` |
| 卡牌 / 回合制 | 状态同步（简单） | `cn.etetet.statesync`（精简依赖） |
| 射击 / FPS | 状态同步（带预测） | `cn.etetet.statesync`（需改造） |

**如果确实需要双同步方式：**
1. 等待 ET 官方把 lockstep 升级到 4.0.0
2. 或自己基于 statesync 4.0.0 创建新主包，把 lockstep 的核心逻辑（truesync + lsentity）移植过来
3. 在不同场景内切换同步系统，不要在同一场景混用

#### 一句话总结

> **lockstep 和 statesync 在 ET 中是两个独立的主包，版本不兼容（3.x vs 4.x），同步架构完全不同。不要尝试在一个主包或一个场景内混用。根据游戏类型选一种即可。**

---

## 十一、相关文档

- [ET-master/AGENTS.md](./ET-master/AGENTS.md) - 项目根目录最小入口规则
- [ET-master/Packages/cn.etetet.harness/AGENTS.md](./ET-master/Packages/cn.etetet.harness/AGENTS.md) - 主要 AI 开发规范、包层级关系、核心原则
- [ET-master/README.md](./ET-master/README.md) - ET 框架 README
- [ET-master/Book/](./ET-master/Book/) - ET 框架教程文档（包含协程、Entity、EventSystem、Actor 模型等）
- [ET-master/Scripts/Initialize-Project.ps1](./ET-master/Scripts/Initialize-Project.ps1) - 脚本本身（带逐行中文注释）

---

## 附录：核心流程总览

```
Initialize-Project.ps1 执行流程
│
├── 步骤 1：解析初始化参数
│   ├── SceneName   ← 命令行 / GlobalConfig.asset
│   ├── MainPackage ← 命令行 / 由 SceneName 推导
│   └── CodeMode    ← 命令行 / GlobalConfig.asset / 默认 ClientServer
│
├── 步骤 2：主包初始化
│   ├── 解析主包 package.json（含直接依赖列表）
│   ├── 生成 MainPackage.txt（裁剪清单）
│   └── 硬链接主包 ET.sln 到根目录
│
├── 步骤 3：-GenerateMainPackageOnly 短路检查
│   └── 是 → 退出；否 → 继续
│
├── 步骤 4：环境检查
│   ├── 目录 / Git / .NET SDK >= 10
│   └── Luban.sln / ET.CodeMode.csproj / ET.SourceGenerator.csproj / ET.sln
│
├── 步骤 5：编译流程
│   ├── 5.1 编译 Luban（配置表工具）
│   ├── 5.2 编译 ET.CodeMode（CodeMode 切换工具）
│   ├── 5.3 编译 ET.SourceGenerator（源生成器）
│   ├── 5.4 执行 ET.CodeMode.dll --CodeMode=xxx（修改 asmref）
│   └── 5.5 （可选）编译整个 ET.sln
│
├── 步骤 6：还原 SourceGenerator .meta 文件
│   └── git restore 保持 GUID 稳定
│
└── 步骤 7：输出 Unity/Rider 人工步骤提示
    └── Unity 版本 / 插件 / IL2CPP / Rider 配置 / 入口场景
```
