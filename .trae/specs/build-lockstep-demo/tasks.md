# Tasks

- [x] Task 1: 创建Unity项目骨架与ET包引用
  - [x] SubTask 1.1: 在 `d:\Unity\LockstepDemo\Asystem` 创建Unity 2022.3项目结构（ProjectSettings、Packages等目录）
  - [x] SubTask 1.2: 编写 `Packages/manifest.json`，以 `file:../../ET/Packages/xxx` 方式引用ET基础设施包（core/sourcegenerator/memorypack/truesync/lsentity/loader/init等）
  - [x] SubTask 1.3: 创建 `Directory.Build.props` 和 `.gitignore`

- [x] Task 2: 创建主包 cn.asystem.lockstep（定义asmdef和ET.sln）
  - [x] SubTask 2.1: 创建 `Packages/cn.asystem.lockstep/package.json`（声明依赖）
  - [x] SubTask 2.2: 在 `Runtime/` 下创建6个asmdef（ET.Model/ET.Hotfix/ET.ModelView/ET.HotfixView/ET.Config/ET.Editor），ET.Hotfix和ET.Config设置noEngineReferences:true
  - [x] SubTask 2.3: 创建 `DotNet~/App/Program.cs`（服务端入口）
  - [x] SubTask 2.4: 创建 `ET.sln` 解决方案文件
  - [x] SubTask 2.5: 创建 `Assets/Scenes/Init.unity` 初始场景

- [x] Task 3: 创建业务包 cn.asystem.battle（战斗与角色控制）
  - [x] SubTask 3.1: 创建 `package.json`（依赖core/truesync/lsentity/proto/config）
  - [x] SubTask 3.2: 创建 `Scripts/Model/Share/` 下的数据模型：Room.cs、FrameBuffer.cs、LSInput.cs、OneFrameInputs.cs、LSUnit.cs、LSUnitComponent.cs、Replay.cs、LSConstValue.cs、PackageType.cs、SceneType.cs
  - [x] SubTask 3.3: 创建 `Scripts/Model/Client/` 下的客户端数据：LSClientUpdater.cs、EventType.cs、WaitType.cs
  - [x] SubTask 3.4: 创建 `Scripts/Model/Server/` 下的服务端数据：RoomServerComponent.cs、LSServerUpdater.cs、MatchComponent.cs、RoomPlayer.cs
  - [x] SubTask 3.5: 创建 `Scripts/Hotfix/Share/` 下的共享逻辑：RoomSystem.cs、LSInputComponentSystem.cs、LSUnitFactory.cs、FiberInit_LockStep.cs
  - [x] SubTask 3.6: 创建 `Scripts/Hotfix/Client/` 下的客户端逻辑：LSClientUpdaterSystem.cs、OneFrameInputsHandler.cs、Room2C_EnterMapHandler.cs、G2C_ChangeSceneHandler.cs
  - [x] SubTask 3.7: 创建 `Scripts/Hotfix/Server/` 下的服务端逻辑：RoomServerComponentSystem.cs、LSServerUpdaterSystem.cs、MatchComponentSystem.cs、FrameMessageHandler.cs、FiberInit各Fiber
  - [x] SubTask 3.8: 创建 `Scripts/ModelView/Client/` 表现层数据：LSUnitView.cs、LSAnimatorComponent.cs、LSOperaComponent.cs
  - [x] SubTask 3.9: 创建 `Scripts/HotfixView/Client/` 表现层逻辑：LSUnitViewSystem.cs、LSAnimatorComponentSystem.cs、LSOperaComponentSystem.cs、LSCameraComponentSystem.cs

- [x] Task 4: 创建业务包 cn.asystem.scene（场景管理）
  - [x] SubTask 4.1: 创建 `package.json`（依赖core/battle）
  - [x] SubTask 4.2: 创建 `Scripts/Model/Client/CurrentScenesComponent.cs`
  - [x] SubTask 4.3: 创建 `Scripts/Hotfix/Client/LSSceneChangeHelper.cs`、`LSSceneChangeStart_AddComponent.cs`
  - [x] SubTask 4.4: 创建 `Scripts/HotfixView/Client/CurrentScenesComponentSystem.cs`、`AfterCreateClientScene_LSAddComponent.cs`

- [x] Task 5: 定义Proto消息协议
  - [x] SubTask 5.1: 创建 `cn.asystem.battle/Proto/` 下的proto文件：LockStepOuter_C_11001.proto（C2G_Match等）、LockStepInner_S_21001.proto（OneFrameInputs、Room2C_EnterMap等）
  - [x] SubTask 5.2: 配置proto生成脚本（参考ET的Luban/Proto生成流程）

- [x] Task 6: 实现角色控制器（移动/跳跃/技能）
  - [x] SubTask 6.1: 扩展LSInput结构：V(TSVector2移动向量)、Jump(bool)、SkillId(int)、SkillDir(TSVector2)
  - [x] SubTask 6.2: 实现LSUnitComponentSystem.Update：根据输入计算定点数位移、跳跃物理、技能触发
  - [x] SubTask 6.3: 定义3种技能配置（普攻近战、位移突进、范围AOE），使用配置表或常量定义
  - [x] SubTask 6.4: 实现LSOperaComponentSystem：采集键盘输入生成LSInput

- [x] Task 7: 实现帧同步网络通信
  - [x] SubTask 7.1: 实现Gate Fiber的C2G_MatchHandler，转发到Match Fiber
  - [x] SubTask 7.2: 实现Match Fiber的MatchComponentSystem：2人匹配成功后通知Map创建Room
  - [x] SubTask 7.3: 实现Map Fiber的RoomManagerComponent：创建Room Fiber并通知玩家进房
  - [x] SubTask 7.4: 实现Room Fiber的RoomServerComponentSystem：管理玩家、转发帧输入
  - [x] SubTask 7.5: 实现LSServerUpdaterSystem：20hz固定间隔从FrameBuffer取帧广播
  - [x] SubTask 7.6: 实现LSClientUpdaterSystem：接收帧输入驱动RoomSystem.Update
  - [x] SubTask 7.7: 实现OneFrameInputsHandler：客户端处理收到的帧输入

- [x] Task 8: 实现场景管理与多玩家同步
  - [x] SubTask 8.1: 实现服务端RoomServerComponent：维护RoomPlayer列表，玩家进房/离场处理
  - [x] SubTask 8.2: 实现客户端LSSceneChangeHelper：场景切换流程（发送ChangeScene→加载场景→通知服务端完成）
  - [x] SubTask 8.3: 实现LSUnitView创建：场景加载完成后为每个LSUnit创建表现层GameObject

- [x] Task 9: 配置StartConfig与运行时配置
  - [x] SubTask 9.1: 创建StartConfig配置文件（Machine/Process/Scene配置，参考ET的StartConfig）
  - [x] SubTask 9.2: 创建GlobalConfig.asset（设置CodeMode、SceneName等）
  - [x] SubTask 9.3: 配置StartProcessConfig（Gate/Match/Map/Room进程）

- [x] Task 10: 编写开发流程文档
  - [x] SubTask 10.1: 编写 `开发流程文档.md`，包含：环境搭建（Unity版本/dotnet版本）、模块开发顺序（骨架→数据模型→逻辑→网络→表现层）、关键技术点（CodeMode切换/asmref生成/帧同步原理/HybridCLR热更）
  - [x] SubTask 10.2: 包含搭建步骤：初始化项目→设置主包→切换CodeMode→编译→运行的完整流程

- [x] Task 11: 编写开发思路说明文档
  - [x] SubTask 11.1: 编写 `开发思路说明文档.md`，包含：架构设计（为什么用ET的All-in-One+CodeMode）、网络同步机制（帧同步 vs 状态同步对比、确定性原理、HashCheck机制）、角色控制实现（定点数物理、输入采集→服务端聚合→广播→客户端回放）、技术选型理由（为什么用TrueSync、为什么用LSEntity）
  - [x] SubTask 11.2: 包含可能遇到的问题：浮点数不确定性、网络延迟抖动、断线重连、性能优化

- [x] Task 12: 验证Demo可运行
  - [x] SubTask 12.1: 结构性验证：所有asmdef/源码/proto/配置/文档文件存在且内容正确
  - [x] SubTask 12.2: 关键代码验证：LSInput含V/Jump/SkillId/SkillDir，ET.Hotfix的noEngineReferences=true，LSInputComponentSystem含移动/跳跃/技能逻辑
  - [x] SubTask 12.3: manifest.json验证：以file:方式引用21个ET包+3个本地业务包

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 2]
- [Task 4] depends on [Task 3]
- [Task 5] depends on [Task 3]
- [Task 6] depends on [Task 3]
- [Task 7] depends on [Task 5]
- [Task 8] depends on [Task 4]
- [Task 9] depends on [Task 2]
- [Task 10] depends on [Task 1]
- [Task 11] depends on [Task 1]
- [Task 12] depends on [Task 7, Task 8, Task 9]
