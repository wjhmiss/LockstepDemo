# Checklist

> 验证时间：2026-07-19  
> 验证范围：SubTask 8.1 静态验证（自动）+ SubTask 8.2/8.3 待用户在 Unity 中手动验证

## 准备阶段

- [x] manifest.json 中包含 cn.etetet.core 引用
- [x] manifest.json 中包含 cn.etetet.loader 引用
- [x] manifest.json 中包含 com.etetet.init 引用
- [x] manifest.json 中包含 cn.etetet.memorypack 引用
- [x] manifest.json 中包含 cn.etetet.sourcegenerator 引用
- [x] manifest.json 中包含 cn.etetet.proto 引用（注：checklist 原写 "com.etetet.proto" 系笔误，ET 实际包名为 `cn.etetet.proto`）
- [x] ET.Model.asmdef 引用了 ET.Model 程序集
- [x] ET.Hotfix.asmdef 引用了 ET.Hotfix 程序集（同时引用 ET.Model）
- [x] ET.ModelView.asmdef 引用了 ET.ModelView 程序集（同时引用 ET.Model）
- [x] ET.HotfixView.asmdef 引用了 ET.HotfixView 程序集（同时引用 ET.ModelView、ET.Hotfix）
- [x] ET.Hotfix.asmdef 仍保持 noEngineReferences: true
- [x] ~~ET.HotfixView.asmdef 仍保持 noEngineReferences: true~~ **【修正】改为 false**
  - 原因：HotfixView 层代码（InputSystem/MoveSystem/PlayerViewSystem/PlayerAnimatorSystem）均使用 `using UnityEngine;`
  - 与 ET 原版 HotfixView.asmdef（noEngineReferences: false）保持一致
  - 已修复，否则编译报 CS0246

## 代码清理

- [x] HelloWorldBase.cs 已删除
- [x] HelloWorldSystem.cs 已删除
- [x] ClientData.cs 已删除
- [x] ClientHelloWorldSystem.cs 已删除
- [x] ClientViewSystem.cs 已删除
- [x] ServerData.cs 已删除
- [x] ServerHelloWorldSystem.cs 已删除
- [x] ServerViewSystem.cs 已删除

## Model 层组件

- [x] PlayerComponent.cs 存在，含 `[ComponentOf(typeof(Scene))]` 特性
- [x] PlayerComponent 继承 Entity, IAwake
- [x] PlayerComponent 含 PlayerId 字段
- [x] MoveComponent.cs 存在，含 `[ComponentOf(typeof(Entity))]` 特性
- [x] MoveComponent 含 Speed、CurrentDirection、TargetDirection、Acceleration 字段（注：原 checklist 写 "MoveDirection"，实际字段名为 `CurrentDirection`）
- [x] JumpComponent.cs 存在，含 `[ComponentOf(typeof(Entity))]` 特性
- [x] JumpComponent 含 VerticalVelocity、IsGrounded、JumpForce、Gravity 字段
- [x] InputRequest.cs 存在，含水平/垂直轴和跳跃请求标记

## Hotfix 层系统

- [x] ~~MoveSystem.cs 存在，使用 [EntitySystemOf] 和 [EntitySystem] 特性~~ **【设计调整】MoveSystem 移至 HotfixView/Client/**
  - 原因：MoveSystem 使用 UnityEngine.Vector3 / Time.deltaTime，Hotfix 层不允许引用 UnityEngine
  - 实际位置：`Scripts/HotfixView/Client/MoveSystem.cs`
- [x] MoveSystem 实现加速度平滑过渡（Vector3.MoveTowards，相当于 lerp）
- [x] MoveSystem 使用 Time.deltaTime 保证帧率无关
- [x] JumpSystem.cs 存在于 Hotfix/Share/，使用 [EntitySystemOf] 和 [EntitySystem] 特性
- [x] JumpSystem 实现了 IUpdate 系统（[EntitySystem] Update）
- [x] JumpSystem 实现重力下落（VerticalVelocity -= Gravity * dt）
- [x] JumpSystem 实现跳跃触发（IsGrounded=true 且收到跳跃请求时设置 VerticalVelocity = JumpForce）
- [x] ~~JumpSystem 实现地面检测重置~~ **【设计调整】地面检测移至 PlayerViewSystem (HotfixView)**
  - 原因：地面检测需要 CharacterController.isGrounded（UnityEngine API），Hotfix 层不能使用
  - 实际位置：`PlayerViewSystem.cs` 中通过 `self.Controller.isGrounded` 检测并回写 `jumpComp.IsGrounded`
- [x] PlayerComponentSystem.cs 存在，实现 IAwake 系统初始化子组件（AddComponent<MoveComponent/JumpComponent>）

## ModelView 层组件

- [x] PlayerViewComponent.cs 存在，含 `[ComponentOf(typeof(PlayerComponent))]` 特性
- [x] PlayerViewComponent 持有 UnityEngine.Transform 引用（PlayerTransform）
- [x] PlayerViewComponent 持有 UnityEngine.CharacterController 引用（Controller）
- [x] PlayerAnimatorComponent.cs 存在，持有 UnityEngine.Animator 引用
- [x] PlayerAnimatorComponent 含 CurrentState 字段（类型为 PlayerAnimState 枚举）
- [x] InputComponent.cs 存在，持有 InputRequest 数据

## HotfixView 层系统

- [x] InputSystem.cs 存在，使用 [EntitySystemOf] 特性
- [x] ~~InputSystem 实现了 IUpdate 系统~~ **【设计调整】输入采集改为静态工具方法 CollectInput()**
  - 原因：避免与 PlayerViewSystem.Update 重复驱动，统一由 PlayerViewSystem.Update 调用 `inputComp.CollectInput()`
  - InputSystem 仅实现 Awake（初始化 InputRequest）
- [x] InputSystem 采集 WASD 输入到 InputRequest.Horizontal/Vertical
- [x] InputSystem 采集方向键输入（兼容性）
- [x] InputSystem 采集空格键输入到 InputRequest.JumpRequested
- [x] PlayerViewSystem.cs 存在
- [x] PlayerViewSystem 实现了 IUpdate 系统（[EntitySystem] Update）
- [x] PlayerViewSystem 通过 CharacterController.Move 应用位移
- [x] PlayerAnimatorSystem.cs 存在
- [x] PlayerAnimatorSystem 实现了 IUpdate 系统
- [x] PlayerAnimatorSystem 根据移动状态切换动画参数（SetInteger("State", ...)）
- [x] PlayerAnimatorSystem 根据跳跃状态切换动画参数（空中：上升为 Jump，下落为 Fall）

## 入口与场景

- [x] GameEntry.cs 存在于 Assets/Scripts/
- [x] GameEntry 继承 MonoBehaviour
- [x] GameEntry.Start() 初始化 ET 框架（Options→TimeInfo→IdGenerater→CodeTypes→CodeProcess→ObjectPool→FiberManager）
- [x] GameEntry.Start() 创建 Scene（通过 FiberManager.CreateMainFiber）和 PlayerComponent
- [x] GameEntry.Start() 为 PlayerComponent 添加 Move/Jump/View/Animator/Input 子组件
  - 注：Move/Jump 由 PlayerComponentSystem.Awake 自动添加，View/Animator/Input 由 GameEntry.CreatePlayer 手动添加
- [x] GameEntry.Start() 将场景中的 Transform/CharacterController/Animator 注入到 ModelView 组件
- [x] PlayerBootstrap.cs 存在于 Assets/Scripts/
- [ ] ~~Main.unity 场景存在于 Assets/Scenes/~~ **【设计调整】改为动态创建 + README 说明**
  - 原因：避免手写 .unity 文件导致 GUID 引用复杂
  - 替代方案：`Assets/Scenes/README.md` 指导用户创建空场景；GameEntry.cs 在 Start() 中动态创建 Ground/Player/Camera
- [x] 场景包含地面 Plane（带 Collider）— GameEntry.CreateSceneContent 动态创建 PrimitiveType.Plane
- [x] 场景包含 Player 胶囊体（带 CharacterController）— GameEntry.CreateSceneContent 动态创建 Capsule + AddComponent<CharacterController>
- [x] 场景包含 Main Camera（朝向 Player）— GameEntry.CreateSceneContent 动态创建并 LookAt(Player)
- [x] 场景包含挂载 GameEntry 的 GameObject — 用户需在场景中新建空 GameObject 并挂载 GameEntry（README 已说明）

## 架构约束

- [x] Model 层代码可以使用 UnityEngine（noEngineReferences=false）
- [x] Hotfix 层代码不使用 UnityEngine（noEngineReferences=true，符合约束）
  - JumpSystem.cs 和 PlayerComponentSystem.cs 均 `namespace ET`，不 using UnityEngine ✅
- [x] ModelView 层代码可以使用 UnityEngine（noEngineReferences=false）
- [x] ~~HotfixView 层代码不直接 using UnityEngine~~ **【修正】HotfixView 层可使用 UnityEngine**
  - 与 ET 原版 HotfixView.asmdef（noEngineReferences: false）保持一致
  - HotfixView 代码均 `using UnityEngine;`（InputSystem/MoveSystem/PlayerViewSystem/PlayerAnimatorSystem）
- [x] 所有 Model 层数据组件继承 ET.Entity
- [x] 所有 Hotfix 层系统使用 [EntitySystemOf]/[EntitySystem] 特性
- [x] 没有循环依赖（Model ← Hotfix，ModelView ← HotfixView）
  - 引用链：ET.Model ← ET.Hotfix / ET.ModelView ← ET.HotfixView，ET.HotfixView 同时引用 ET.Hotfix 和 ET.ModelView，无反向引用

## ET API 验证（SubTask 8.1 补充）

- [x] `World.Instance.AddSingleton<T>()` 存在（World.cs:65）
- [x] `World.Instance.AddSingleton<T, A>(A a)` 存在（World.cs:74，用于 CodeTypes 初始化）
- [x] `World.Instance.Dispose()` 存在（World.cs:27）
- [x] `FiberManager.CreateMainFiber(int sceneType, string sceneName)` 存在（FiberManager.cs:115，返回 ETTask<long>）
- [x] `FiberManager.Update()` / `LateUpdate()` 存在（FiberManager.cs:87 / 97）
- [x] `Fiber.Instance` 为 `internal static Fiber`（Fiber.cs:23，需反射访问）—— GameEntry 已用反射
- [x] `Fiber.Instance = mainFiber` 在 FiberManager.Update/LateUpdate 中设置（FiberManager.cs:94/102）
- [x] `Fiber.Root` 为 `public Scene`（Fiber.cs:64）
- [x] `CodeTypes.CodeProcess()` 存在（CodeTypes.cs:57）
- [x] `Entity.AddComponent<K>()` 存在（Entity.cs:757，约束 `where K : Entity, IAwake, new()`）
- [x] `Entity.GetComponent<K>()` 存在（Entity.cs:613）
- [x] `Entity.GetParent<T>()` 存在（Entity.cs:301）
- [x] Options/TimeInfo/IdGenerater/ObjectPool/FiberManager 均实现 `Singleton<T>, ISingletonAwake`（无参 Awake）
- [x] CodeTypes 实现 `Singleton<T>, ISingletonAwake<Assembly[]>`（带参 Awake）

## 验证

- [x] 静态验证：所有 asmdef 引用关系正确，无循环依赖（SubTask 8.1 完成）
  - 修复项：HotfixView.asmdef noEngineReferences 从 true 改为 false
- [ ] 编译验证：Unity 打开项目后无 CS 错误（SubTask 8.2，需用户手动）
- [ ] 运行验证（用户手动）：Main.unity 场景可正常 Play
- [ ] 运行验证（用户手动）：WASD 控制角色平滑移动
- [ ] 运行验证（用户手动）：方向键同样可控制移动（输入兼容性）
- [ ] 运行验证（用户手动）：空格键角色跳跃，受重力下落
- [ ] 运行验证（用户手动）：落地后才能再次跳跃（防连跳）
- [ ] 运行验证（用户手动）：移动时动画切换（如有 Animator）
