# 角色控制模块 Spec

## Why
CodeMode Demo 当前只是 asmdef 机制演示，没有任何可运行的游戏内容。为了让用户能在 Unity 中实际运行项目、体验 ET 框架的组件化设计思想，需要添加一个基础的角色控制模块作为迭代开发的起点，实现键盘控制角色移动和跳跃。

## What Changes
- **BREAKING**: 修改 `Packages/manifest.json`，引入 ET 基础设施包（cn.etetet.core 等），使项目具备真正的 ET 运行时
- **BREAKING**: 修改 `cn.codemode.demo/Runtime/*.asmdef` 和 `cn.codemode.helloworld/Scripts` 下的 asmref 引用关系，使其引用 ET 程序集
- 新增 `cn.codemode.helloworld/Scripts/Model/Share/` 下的角色数据组件：`PlayerComponent`、`MoveComponent`、`JumpComponent`
- 新增 `cn.codemode.helloworld/Scripts/Hotfix/Share/` 下的角色逻辑系统：`MoveSystem`、`JumpSystem`
- 新增 `cn.codemode.helloworld/Scripts/ModelView/Client/` 下的表现层组件：`PlayerViewComponent`、`PlayerAnimatorComponent`
- 新增 `cn.codemode.helloworld/Scripts/HotfixView/Client/` 下的表现层逻辑：`PlayerViewSystem`（同步 Transform）、`InputSystem`（采集键盘输入）、`PlayerAnimatorSystem`（动画切换）
- 新增 `Assets/Scripts/GameEntry.cs` MonoBehaviour 入口脚本（初始化 ET 框架并创建玩家实体）
- 新增 `Assets/Scenes/Main.unity` 主场景（包含地面、玩家胶囊体、摄像机）
- 移除现有 `HelloWorldBase`/`HelloWorldSystem` 等演示代码（已被角色控制代码取代）

## Impact
- Affected specs: `explain-codemode-architecture`（asmdef 机制不变，但项目用途从纯演示变为可运行）
- Affected code:
  - `Packages/manifest.json`（新增 ET 包依赖）
  - `Packages/cn.codemode.demo/Runtime/Model/ET.Model.asmdef`（引用 ET.Model）
  - `Packages/cn.codemode.demo/Runtime/Hotfix/ET.Hotfix.asmdef`（引用 ET.Hotfix）
  - `Packages/cn.codemode.demo/Runtime/ModelView/ET.ModelView.asmdef`（引用 ET.ModelView）
  - `Packages/cn.codemode.demo/Runtime/HotfixView/ET.HotfixView.asmdef`（引用 ET.HotfixView）
  - `Packages/cn.codemode.helloworld/Scripts/**`（删除 HelloWorld，新增角色控制）

## ADDED Requirements

### Requirement: 引入 ET 基础设施包
系统 SHALL 在 manifest.json 中引用 ET 框架的核心基础设施包，使项目具备真正的 Entity/Component/System 运行时能力。

#### Scenario: 包引用配置正确
- **WHEN** Unity 加载项目
- **THEN** manifest.json 中应包含对 cn.etetet.core、cn.etetet.loader、com.etetet.init、cn.etetet.memorypack、cn.etetet.sourcegenerator、com.etetet.proto 等包的引用
- **AND** 所有包应成功解析，无依赖错误

### Requirement: 角色数据组件（Model 层）
系统 SHALL 提供符合 ET 组件化设计的角色数据组件，包括玩家身份、移动状态、跳跃状态。

#### Scenario: PlayerComponent 组件定义
- **WHEN** 定义 PlayerComponent
- **THEN** 应使用 `[ComponentOf(typeof(Scene))]` 特性
- **AND** 应继承 `Entity, IAwake` 接口
- **AND** 应包含玩家ID字段（PlayerId）

#### Scenario: MoveComponent 组件定义
- **WHEN** 定义 MoveComponent
- **THEN** 应使用 `[ComponentOf(typeof(PlayerComponent))]` 或 Entity 基类
- **AND** 应包含移动速度（Speed）、当前移动方向（MoveDirection，UnityEngine.Vector3）、目标方向
- **AND** 应支持移动平滑度（加速度/减速度参数）

#### Scenario: JumpComponent 组件定义
- **WHEN** 定义 JumpComponent
- **THEN** 应包含垂直速度（VerticalVelocity）、是否在地面（IsGrounded）、跳跃力度（JumpForce）、重力系数（Gravity）

### Requirement: 角色逻辑系统（Hotfix 层）
系统 SHALL 提供角色移动和跳跃的逻辑系统，按 ET System 模式实现。

#### Scenario: MoveSystem 移动逻辑
- **WHEN** MoveSystem.Update 执行
- **THEN** 应根据 MoveComponent.MoveDirection 和 Speed 计算位移
- **AND** 应支持加速度平滑过渡（当前方向 → 目标方向）
- **AND** 位移计算应使用 Time.deltaTime 保证帧率无关

#### Scenario: JumpSystem 跳跃逻辑
- **WHEN** JumpSystem.Update 执行
- **THEN** 若 IsGrounded=false，应按 Gravity 累加 VerticalVelocity
- **AND** 若 IsGrounded=true 且收到跳跃输入，应设置 VerticalVelocity = JumpForce
- **AND** 当垂直位移使角色回到地面时，应重置 VerticalVelocity=0 并 IsGrounded=true

### Requirement: 表现层组件（ModelView 层）
系统 SHALL 提供持有 UnityEngine 对象引用的表现层组件，作为 HotfixView 操控 Unity 引擎的桥梁。

#### Scenario: PlayerViewComponent 组件定义
- **WHEN** 定义 PlayerViewComponent
- **THEN** 应持有 `UnityEngine.Transform` 引用（PlayerTransform）
- **AND** 应持有 `UnityEngine.CharacterController` 引用（Controller）
- **AND** 应使用 `[ComponentOf]` 特性声明归属

#### Scenario: PlayerAnimatorComponent 组件定义
- **WHEN** 定义 PlayerAnimatorComponent
- **THEN** 应持有 `UnityEngine.Animator` 引用
- **AND** 应包含当前动画状态字段（CurrentState）

### Requirement: 表现层逻辑系统（HotfixView 层）
系统 SHALL 提供输入采集、Transform 同步、动画切换等表现层逻辑。

#### Scenario: InputSystem 输入采集
- **WHEN** InputSystem.Update 执行
- **THEN** 应采集键盘 WASD/方向键输入
- **AND** 应采集空格键跳跃输入
- **AND** 应将输入写入 MoveComponent.MoveDirection 和 JumpComponent 的跳跃请求标记
- **AND** 应支持输入设备兼容性（通过 Unity Input System 或 Input.GetKey）

#### Scenario: PlayerViewSystem Transform 同步
- **WHEN** PlayerViewSystem.Update 执行
- **THEN** 应读取 MoveSystem 计算的水平位移
- **AND** 应读取 JumpSystem 计算的垂直位移
- **AND** 应通过 CharacterController.Move 应用位移到 PlayerTransform

#### Scenario: PlayerAnimatorSystem 动画切换
- **WHEN** PlayerAnimatorSystem.Update 执行
- **THEN** 应根据 MoveComponent.MoveDirection 切换 Idle/Move 动画
- **AND** 应根据 JumpComponent.IsGrounded 切换 Jump/Fall 动画

### Requirement: Unity 入口场景与 MonoBehaviour 启动
系统 SHALL 提供 Unity 场景和 MonoBehaviour 入口，用于初始化 ET 框架并创建玩家实体。

#### Scenario: Main.unity 场景结构
- **WHEN** 打开 Main.unity 场景
- **THEN** 应包含一个地面 Plane（带 Collider）
- **AND** 应包含一个 Player 胶囊体（带 CharacterController）
- **AND** 应包含一个 Main Camera（朝向 Player）
- **AND** 应包含一个 GameObject 挂载 GameEntry.cs

#### Scenario: GameEntry.cs 入口逻辑
- **WHEN** 场景启动
- **THEN** GameEntry.Start() 应初始化 ET 框架
- **AND** 应创建 Scene 和 PlayerComponent 实体
- **AND** 应为 PlayerComponent 添加 MoveComponent、JumpComponent、PlayerViewComponent、PlayerAnimatorComponent
- **AND** 应将场景中的 Player GameObject 的 Transform/CharacterController/Animator 注入到 ModelView 组件

### Requirement: 输入设备兼容性与基础游戏体验
系统 SHALL 保证输入响应灵敏、移动平滑、跳跃物理自然。

#### Scenario: 移动平滑度
- **WHEN** 玩家按住 W 键
- **THEN** 角色应从静止平滑加速到最大速度（通过 lerp 实现）
- **AND** 松开按键后应平滑减速到 0
- **AND** 不应出现瞬间启停的卡顿感

#### Scenario: 跳跃物理效果
- **WHEN** 玩家按空格键
- **THEN** 角色应获得向上的初速度
- **AND** 在空中应受重力影响做抛物线运动
- **AND** 落地时应正确检测地面并重置 IsGrounded
- **AND** 落地后才能再次跳跃（避免连跳）

#### Scenario: 输入设备兼容性
- **WHEN** 使用键盘
- **THEN** WASD 和方向键都应能控制移动
- **AND** 空格键应能触发跳跃

## MODIFIED Requirements

### Requirement: 业务包代码内容
原 HelloWorld 演示代码 SHALL 被角色控制业务代码取代，但目录结构和 asmdef/asmref 机制保持不变。

## REMOVED Requirements

### Requirement: HelloWorld 演示代码
**Reason**: 已完成其架构演示作用，被角色控制业务代码取代
**Migration**: 删除 `Scripts/Model/Share/HelloWorldBase.cs`、`Scripts/Hotfix/Share/HelloWorldSystem.cs`、`Scripts/Model/Client/ClientData.cs`、`Scripts/Hotfix/Client/ClientHelloWorldSystem.cs`、`Scripts/HotfixView/Client/ClientViewSystem.cs`、`Scripts/Model/Server/ServerData.cs`、`Scripts/Hotfix/Server/ServerHelloWorldSystem.cs`、`Scripts/HotfixView/Server/ServerViewSystem.cs`
