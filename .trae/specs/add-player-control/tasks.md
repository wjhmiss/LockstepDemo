# Tasks

- [x] Task 1: 引入 ET 基础设施包
  - [x] SubTask 1.1: 修改 `Packages/manifest.json`，新增 cn.etetet.core、cn.etetet.loader、com.etetet.init、cn.etetet.memorypack、cn.etetet.sourcegenerator、cn.etetet.proto 等 6 个 ET 包引用
  - [x] SubTask 1.2: 修改 `cn.codemode.demo/Runtime/Model/ET.Model.asmdef`，新增对 ET.Model 的引用
  - [x] SubTask 1.3: 修改 `cn.codemode.demo/Runtime/Hotfix/ET.Hotfix.asmdef`，新增对 ET.Hotfix 的引用
  - [x] SubTask 1.4: 修改 `cn.codemode.demo/Runtime/ModelView/ET.ModelView.asmdef`，新增对 ET.ModelView 的引用
  - [x] SubTask 1.5: 修改 `cn.codemode.demo/Runtime/HotfixView/ET.HotfixView.asmdef`，新增对 ET.HotfixView 的引用

- [x] Task 2: 清理 HelloWorld 演示代码
  - [x] SubTask 2.1: 删除 `cn.codemode.helloworld/Scripts/Model/Share/HelloWorldBase.cs`
  - [x] SubTask 2.2: 删除 `cn.codemode.helloworld/Scripts/Hotfix/Share/HelloWorldSystem.cs`
  - [x] SubTask 2.3: 删除 `cn.codemode.helloworld/Scripts/Model/Client/ClientData.cs`
  - [x] SubTask 2.4: 删除 `cn.codemode.helloworld/Scripts/Hotfix/Client/ClientHelloWorldSystem.cs`
  - [x] SubTask 2.5: 删除 `cn.codemode.helloworld/Scripts/HotfixView/Client/ClientViewSystem.cs`
  - [x] SubTask 2.6: 删除 `cn.codemode.helloworld/Scripts/Model/Server/ServerData.cs`
  - [x] SubTask 2.7: 删除 `cn.codemode.helloworld/Scripts/Hotfix/Server/ServerHelloWorldSystem.cs`
  - [x] SubTask 2.8: 删除 `cn.codemode.helloworld/Scripts/HotfixView/Server/ServerViewSystem.cs`

- [x] Task 3: 实现 Model 层角色数据组件
  - [x] SubTask 3.1: 创建 `Scripts/Model/Share/PlayerComponent.cs`，定义 `[ComponentOf(typeof(Scene))]` PlayerComponent，继承 Entity, IAwake，含 PlayerId 字段
  - [x] SubTask 3.2: 创建 `Scripts/Model/Share/MoveComponent.cs`，定义 `[ComponentOf(typeof(Entity))]` MoveComponent，含 Speed、CurrentDirection、TargetDirection、Acceleration 等字段
  - [x] SubTask 3.3: 创建 `Scripts/Model/Share/JumpComponent.cs`，定义 `[ComponentOf(typeof(Entity))]` JumpComponent，含 VerticalVelocity、IsGrounded、JumpForce、Gravity 字段
  - [x] SubTask 3.4: 创建 `Scripts/Model/Share/InputRequest.cs`，定义输入请求结构（水平/垂直轴、跳跃请求标记）

- [x] Task 4: 实现 Hotfix 层角色逻辑系统
  - [x] SubTask 4.1: ~~创建 `Scripts/Hotfix/Share/MoveSystem.cs`~~ **【设计调整】移至 HotfixView/Client/MoveSystem.cs**（MoveSystem 需使用 UnityEngine.Vector3/Time.deltaTime）
  - [x] SubTask 4.2: 创建 `Scripts/Hotfix/Share/JumpSystem.cs`，实现 JumpComponent 的 IUpdate 系统，处理重力下落、跳跃触发（地面检测移至 PlayerViewSystem）
  - [x] SubTask 4.3: 创建 `Scripts/Hotfix/Share/PlayerComponentSystem.cs`，实现 PlayerComponent 的 IAwake 系统，初始化 Move/Jump 子组件

- [x] Task 5: 实现 ModelView 层表现层组件
  - [x] SubTask 5.1: 创建 `Scripts/ModelView/Client/PlayerViewComponent.cs`，持有 UnityEngine.Transform 和 CharacterController 引用，使用 `[ComponentOf(typeof(PlayerComponent))]`
  - [x] SubTask 5.2: 创建 `Scripts/ModelView/Client/PlayerAnimatorComponent.cs`，持有 UnityEngine.Animator 引用和 CurrentState 字段
  - [x] SubTask 5.3: 创建 `Scripts/ModelView/Client/InputComponent.cs`，持有 InputRequest 数据（用于 HotfixView 写入、Hotfix 读取）

- [x] Task 6: 实现 HotfixView 层表现层逻辑
  - [x] SubTask 6.1: 创建 `Scripts/HotfixView/Client/InputSystem.cs`，实现 InputComponent 的 Awake 系统 + CollectInput() 静态工具方法，采集 WASD/方向键/空格键写入 InputRequest
  - [x] SubTask 6.2: 创建 `Scripts/HotfixView/Client/PlayerViewSystem.cs`，实现 PlayerViewComponent 的 IUpdate 系统，读取 MoveSystem/JumpSystem 输出，通过 CharacterController.Move 应用位移
  - [x] SubTask 6.3: 创建 `Scripts/HotfixView/Client/MoveSystem.cs`（从 Task 4 迁入），实现 MoveComponent 的 Awake 系统 + UpdateMove() 工具方法
  - [x] SubTask 6.4: 创建 `Scripts/HotfixView/Client/PlayerAnimatorSystem.cs`，实现 PlayerAnimatorComponent 的 IUpdate 系统，根据移动/跳跃状态切换动画参数

- [x] Task 7: 创建 Unity 入口场景与 MonoBehaviour
  - [x] SubTask 7.1: 创建 `Assets/Scripts/GameEntry.cs` MonoBehaviour 入口，在 Start() 中初始化 ET 框架、创建 Scene、添加 PlayerComponent 及其子组件、注入场景引用
  - [x] SubTask 7.2: 创建 `Assets/Scripts/PlayerBootstrap.cs` MonoBehaviour，在场景中标记 Player 物体，便于 GameEntry 查找并注入引用
  - [x] SubTask 7.3: ~~创建 `Assets/Scenes/Main.unity` 场景~~ **【设计调整】改为 `Assets/Scenes/README.md` 说明文档**，GameEntry 在 Start() 中动态创建 Ground/Player/Camera
  - [x] SubTask 7.4: ~~创建 `Assets/Scenes/Main.unity.meta`~~ 不再需要（无 .unity 文件）

- [x] Task 8: 端到端验证
  - [x] SubTask 8.1: 静态验证（自动完成）
    - 检查所有 asmdef 引用关系：正确，无循环依赖
    - 检查 noEngineReferences 约束：Hotfix=true ✅，HotfixView 从 true 修正为 false（与 ET 原版一致）
    - 验证 ET API 调用：World/FiberManager/Fiber/CodeTypes/Entity 等 13 项 API 全部存在
    - 已更新 `checklist.md` 反映验证结果
  - [ ] SubTask 8.2: 编译验证：Unity 打开项目编译通过，无 CS 错误（需用户在 Unity Editor 中验证）
  - [ ] SubTask 8.3: 运行验证（用户手动）：打开 Main.unity → Play → 按 WASD 角色移动 → 按空格角色跳跃 → 角色落地可重跳

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 1]
- [Task 4] depends on [Task 3]
- [Task 5] depends on [Task 1]
- [Task 6] depends on [Task 4, Task 5]
- [Task 7] depends on [Task 4, Task 6]
- [Task 8] depends on [Task 7]
