# Main 场景创建说明

由于 Unity 场景文件（.unity）的 MonoBehaviour 引用需要 .cs 脚本的 GUID（由 Unity 在 .meta 文件中自动生成），手工编写 .unity 文件难以保证 GUID 正确，因此本 Demo 不直接提供 .unity 文件，改为在 Unity 编辑器中手动创建空场景并挂载 GameEntry 脚本。

GameEntry.cs 的 `Start()` 会动态创建所有场景内容（地面、Player、Camera），所以场景本身只需要一个挂载了 GameEntry.cs 的 GameObject。

## 创建步骤

1. 在 Unity 中打开 CodeMode 项目（位于 `CodeMode/` 目录）。
2. 菜单 `File > New Scene`，选择 `Basic (Built-in)` 模板，创建新场景。
3. 将场景保存为 `Assets/Scenes/Main.unity`（若 `Assets/Scenes` 目录不存在，请先创建）。
4. 在 Hierarchy 视图中：
   - 右键 `> Create Empty`，命名为 `GameEntry`。
   - 选中 `GameEntry` 对象，在 Inspector 中点击 `Add Component`，搜索 `GameEntry` 并添加。
   - （可选）若想让 Player 物体在场景初始化前就具备 Animator，可在场景中再创建一个空 GameObject 命名为 `Player`，挂上 `PlayerBootstrap` 脚本。若不挂，GameEntry 仍会动态创建 Player，但 Animator 可能为空。
5. 保存场景（`Ctrl + S`）。
6. 点击 Play 运行。

## 运行后预期效果

GameEntry.Start() 会自动完成：

- 创建地面（Plane，缩放为 20×20）
- 创建 Player（Capsule，附带 CharacterController）
- 调整 Main Camera 的位置与朝向
- 初始化 ET 框架（添加 Options/TimeInfo/IdGenerater/CodeTypes/EventSystem/EntitySystemSingleton/ObjectPool/FiberManager 等单例）
- 创建主 Fiber（内部创建 Scene 作为 Fiber.Root）
- 在 Scene 上创建 PlayerComponent 实体并添加 MoveComponent/JumpComponent（由 PlayerComponentSystem.Awake 触发）
- 注入 PlayerViewComponent（Transform、CharacterController、GroundY）
- 注入 PlayerAnimatorComponent（Animator，可能为空）
- 注入 InputComponent

GameEntry.Update() 每帧调用 `FiberManager.Update()/LateUpdate()` 驱动以下系统：

- `JumpSystem.Update`（Hotfix 层，重力与垂直速度）
- `PlayerViewSystem.Update`（采集输入、应用位移、地面检测、朝向移动方向）
- `PlayerAnimatorSystem.Update`（根据状态切换 Animator 参数）

## 操作方式

- `W/A/S/D` 或方向键：水平移动角色
- `Space`：跳跃

## 常见问题

- 若运行时控制台输出 `[GameEntry] CreateMainFiber failed:`，说明 ET 框架某些单例未初始化完整。GameEntry 已经按依赖顺序添加了关键单例，但完整 ET 还依赖 `CodeLoader` 加载热更程序集——本简化版未集成 CodeLoader，部分系统类（PlayerComponentSystem 等）的注册可能不完整。
- 若 Player 不动，检查 Console 是否有 `[GameEntry] Update error:`，可能是某个 ET 系统抛异常导致 frameworkReady 被关闭。
- Animator 没有AnimatorController 时不会播放动画，但 PlayerAnimatorSystem 仍会调用 SetInteger/SetFloat（不会报错）。
