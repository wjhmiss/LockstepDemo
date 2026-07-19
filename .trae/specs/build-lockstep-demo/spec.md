# 帧同步多人在线游戏Demo Spec

## Why
当前需要基于ET框架基础设施，在 `d:\Unity\LockstepDemo\Asystem` 中从零搭建一套可运行的帧同步多人在线游戏Demo，验证ET架构在新项目中的落地方式，并实现多玩家实时对战的核心玩法（移动/跳跃/技能）。

## What Changes
- 新建Unity项目 `Asystem`，通过 `manifest.json` 以 `file:` 方式引用ET基础设施包（core/sourcegenerator/memorypack/truesync/lsentity/loader/init等）
- 新建主包 `cn.asystem.lockstep`，在其 `Runtime/` 目录下定义 `ET.Model`/`ET.Hotfix`/`ET.ModelView`/`ET.HotfixView`/`ET.Config`/`ET.Editor` 六个asmdef，并提供 `ET.sln`
- 新建业务包 `cn.asystem.battle`（战斗/角色控制）与 `cn.asystem.scene`（场景管理），遵循 `Scripts/{Model,Hotfix,ModelView,HotfixView}/{Share,Client,Server}` 目录规范
- 实现帧同步核心：`Room`/`FrameBuffer`/`LSInput`/`OneFrameInputs`/`LSUnit`/`LSUnitComponent` 数据模型与 `RoomSystem`/`LSClientUpdater`/`LSServerUpdater` 逻辑
- 实现匹配→房间→进房→帧同步对战→断线重连的完整网络流程（Gate/Match/Map/Room 四种服务端Fiber）
- 实现角色控制器：基于 `LSInput`（移动向量/跳跃/技能Id/技能方向）驱动 `LSUnit` 的 `TSVector` 定点数位置与朝向，支持3种技能（普攻/位移技/范围技）
- 实现场景管理：服务端 `RoomServerComponent` 维护房间内所有玩家，客户端 `CurrentScenesComponent` + `LSSceneChangeHelper` 处理场景切换
- 提供两份文档：`开发流程文档.md`（环境搭建/模块开发顺序/关键技术点）与 `开发思路说明文档.md`（架构设计/网络同步/角色控制原理）

## Impact
- 依赖ET参考文档：[ET包依赖关系分析.md](file:///d:/Unity/LockstepDemo/ET/ET包依赖关系分析.md)、[ET新手学习教程.md](file:///d:/Unity/LockstepDemo/ET/ET新手学习教程.md)、[ET架构设计与程序集机制详解.md](file:///d:/Unity/LockstepDemo/ET/ET架构设计与程序集机制详解.md)
- 参考ET原版lockstep包结构：[cn.etetet.lockstep](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.lockstep)
- 产出目录：`d:\Unity\LockstepDemo\Asystem`
- 不修改ET原包代码，仅通过manifest.json引用

## ADDED Requirements

### Requirement: 项目骨架与包引用
系统 SHALL 在 `d:\Unity\LockstepDemo\Asystem` 下建立Unity项目，通过 `Packages/manifest.json` 以 `file:../../ET/Packages/xxx` 方式引用ET基础设施包，不得拷贝ET源码到Asystem。

#### Scenario: 包引用验证
- **WHEN** 打开Asystem的manifest.json
- **THEN** 可见对 `cn.etetet.core`、`cn.etetet.sourcegenerator`、`cn.etetet.memorypack`、`cn.etetet.truesync`、`cn.etetet.lsentity`、`cn.etetet.loader`、`com.etetet.init` 等包的file引用
- **AND** 不存在任何ET源码的拷贝

### Requirement: 主包asmdef定义
系统 SHALL 在 `cn.asystem.lockstep/Runtime/` 下定义6个asmdef文件，程序集名称与ET规范一致（ET.Model/ET.Hotfix/ET.ModelView/ET.HotfixView/ET.Config/ET.Editor），且ET.Hotfix与ET.Config设置 `noEngineReferences:true`。

#### Scenario: asmdef配置正确
- **WHEN** 读取 `Runtime/Hotfix/ET.Hotfix.asmdef`
- **THEN** `noEngineReferences` 为 true
- **AND** references 包含 ET.Core、ET.Model

### Requirement: 业务包目录规范
系统 SHALL 为每个业务包按 `Scripts/{Model,Hotfix,ModelView,HotfixView}/{Share,Client,Server}` 组织代码，并通过package.json声明对ET包的依赖。

#### Scenario: 业务包结构验证
- **WHEN** 检查 `cn.asystem.battle` 包
- **THEN** 存在 `Scripts/Model/Share/`、`Scripts/Hotfix/Share/`、`Scripts/Hotfix/Client/`、`Scripts/Hotfix/Server/` 等目录

### Requirement: 帧同步核心数据模型
系统 SHALL 在Share层定义帧同步核心数据结构：Room、FrameBuffer、LSInput、OneFrameInputs、LSUnit、LSUnitComponent、Replay。

#### Scenario: 帧同步数据模型完整
- **WHEN** 检查 `cn.asystem.battle/Scripts/Model/Share/`
- **THEN** 存在 Room.cs、FrameBuffer.cs、LSInput.cs、OneFrameInputs.cs、LSUnit.cs、LSUnitComponent.cs

### Requirement: 帧同步逻辑系统
系统 SHALL 实现RoomSystem（房间主循环）、LSClientUpdater（客户端帧驱动）、LSServerUpdater（服务端帧驱动）、LSInputComponentSystem（输入收集与回放）。

#### Scenario: 帧同步主循环可运行
- **WHEN** 服务端Room Fiber启动且有2个玩家就绪
- **THEN** LSServerUpdater按固定间隔(20hz)从FrameBuffer取帧并广播OneFrameInputs
- **AND** 客户端LSClientUpdater收到帧输入后调用RoomSystem.Update驱动所有LSUnit

### Requirement: 角色控制系统
系统 SHALL 实现基于LSInput的角色控制器，支持移动（TSVector2方向输入→定点数位移）、跳跃（垂直速度+重力）、3种技能（普攻/位移/范围）。

#### Scenario: 移动同步
- **WHEN** 玩家A按下方向键
- **THEN** LSInput包含V(移动向量)字段
- **AND** 该输入经服务端广播后，所有客户端的LSUnit位置在相同帧次后一致（HashCheck通过）

#### Scenario: 技能释放
- **WHEN** 玩家按下技能键
- **THEN** LSInput包含SkillId(技能Id)和SkillDir(技能方向)
- **AND** 所有客户端在同一帧执行技能逻辑

### Requirement: 场景管理系统
系统 SHALL 实现场景加载与多玩家同步：服务端RoomServerComponent管理房间内所有玩家，客户端通过LSSceneChangeHelper切换场景并加载LSUnit表现层。

#### Scenario: 多玩家进房
- **WHEN** 2个玩家匹配成功
- **THEN** 服务端创建Room Fiber，每个玩家收到Room2C_EnterMap消息
- **AND** 客户端加载战斗场景，为每个玩家创建LSUnitView

### Requirement: 匹配与房间流程
系统 SHALL 实现匹配→房间创建→进房→对战→结束的完整流程，包含Gate/Match/Map/Room四种服务端Fiber。

#### Scenario: 匹配成功
- **WHEN** 2个玩家发送C2G_Match请求
- **THEN** Match Fiber匹配成功后通知Gate，Gate通知玩家
- **AND** Map Fiber创建Room Fiber并通知玩家进房

### Requirement: 文档交付
系统 SHALL 提供两份文档：`开发流程文档.md`（环境搭建、模块开发顺序、关键技术点）和 `开发思路说明文档.md`（架构设计、网络同步机制、角色控制实现、技术选型理由、可能遇到的问题）。

#### Scenario: 文档完整
- **WHEN** 检查Asystem根目录
- **THEN** 存在 `开发流程文档.md` 和 `开发思路说明文档.md`
- **AND** 文档内容覆盖架构、网络同步、角色控制等核心主题

### Requirement: 可运行Demo
系统 SHALL 提供可运行的Demo：服务端可通过 `dotnet Bin/ET.App.dll --Console=1` 启动，客户端可在Unity编辑器Play运行，2个客户端可同时进入同一房间进行帧同步对战。

#### Scenario: 双端可运行
- **WHEN** 启动服务端，再启动2个Unity客户端Play
- **THEN** 2个客户端可匹配进入同一房间
- **AND** 一个客户端的角色移动在另一个客户端可见且状态一致
