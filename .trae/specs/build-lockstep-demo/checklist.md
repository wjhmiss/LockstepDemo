# Checklist

## 项目骨架
- [x] Asystem目录下存在Unity项目结构（ProjectSettings/Packages/Assets）
- [x] manifest.json以file:方式引用ET包，不拷贝ET源码
- [x] 存在Directory.Build.props和.gitignore

## 主包asmdef
- [x] cn.asystem.lockstep/Runtime/Model/ET.Model.asmdef存在且name为"ET.Model"
- [x] cn.asystem.lockstep/Runtime/Hotfix/ET.Hotfix.asmdef存在且noEngineReferences为true
- [x] cn.asystem.lockstep/Runtime/ModelView/ET.ModelView.asmdef存在
- [x] cn.asystem.lockstep/Runtime/HotfixView/ET.HotfixView.asmdef存在
- [x] cn.asystem.lockstep/Runtime/Config/ET.Config.asmdef存在且noEngineReferences为true
- [x] cn.asystem.lockstep/Runtime/Editor/ET.Editor.asmdef存在
- [x] cn.asystem.lockstep/ET.sln存在
- [x] cn.asystem.lockstep/DotNet~/App/Program.cs存在

## 业务包结构
- [x] cn.asystem.battle/package.json声明对core/truesync/lsentity/proto/config的依赖
- [x] cn.asystem.battle/Scripts/Model/Share/存在Room.cs/FrameBuffer.cs/LSInput.cs/OneFrameInputs.cs/LSUnit.cs/LSUnitComponent.cs
- [x] cn.asystem.battle/Scripts/Hotfix/Share/存在RoomSystem.cs/LSInputComponentSystem.cs
- [x] cn.asystem.battle/Scripts/Hotfix/Client/存在LSClientUpdaterSystem.cs/OneFrameInputsHandler.cs
- [x] cn.asystem.battle/Scripts/Hotfix/Server/存在RoomServerComponentSystem.cs/LSServerUpdaterSystem.cs/MatchComponentSystem.cs
- [x] cn.asystem.battle/Scripts/ModelView/Client/存在LSUnitView.cs/LSOperaComponent.cs
- [x] cn.asystem.battle/Scripts/HotfixView/Client/存在LSUnitViewSystem.cs/LSOperaComponentSystem.cs
- [x] cn.asystem.scene/package.json声明对core/battle的依赖
- [x] cn.asystem.scene/Scripts/Model/Client/存在CurrentScenesComponent.cs
- [x] cn.asystem.scene/Scripts/Hotfix/Client/存在LSSceneChangeHelper.cs

## 帧同步核心
- [x] Room类继承Entity并实现帧同步房间数据结构
- [x] FrameBuffer实现环形缓冲区存储帧输入
- [x] LSInput包含V(移动)/Jump(跳跃)/SkillId(技能)/SkillDir(技能方向)字段
- [x] OneFrameInputs包含一帧内所有玩家的输入集合
- [x] RoomSystem.Update方法按帧驱动所有LSUnit的逻辑更新
- [x] LSClientUpdater实现客户端帧驱动（追帧/等待/回放）
- [x] LSServerUpdater实现服务端20hz固定间隔帧广播

## 角色控制
- [x] 移动：LSInput.V转换为TSVector定点数位移
- [x] 跳跃：LSInput.Jump触发垂直速度+重力计算
- [x] 技能1（普攻）：近战攻击逻辑
- [x] 技能2（位移）：突进位移逻辑
- [x] 技能3（范围）：AOE伤害逻辑
- [x] LSOperaComponentSystem采集键盘输入生成LSInput

## 网络通信
- [x] Proto定义C2G_Match/Match2G_NotifyMatchSuccess/Room2C_EnterMap/OneFrameInputs/C2Room_CheckHash等消息
- [x] Gate Fiber处理C2G_Match并转发到Match Fiber
- [x] Match Fiber实现2人匹配逻辑
- [x] Map Fiber创建Room Fiber
- [x] Room Fiber管理玩家列表并广播帧输入
- [x] 客户端OneFrameInputsHandler处理收到的帧输入

## 场景管理
- [x] 服务端RoomServerComponent维护RoomPlayer列表
- [x] 客户端LSSceneChangeHelper处理场景切换流程
- [x] 场景加载完成后为每个LSUnit创建LSUnitView表现层

## 运行配置
- [x] StartConfig配置文件存在（Machine/Process/Scene）
- [x] GlobalConfig.asset存在且配置CodeMode/SceneName（由com.etetet.init提供）
- [x] StartProcessConfig配置Gate/Match/Map/Room进程

## 文档
- [x] 开发流程文档.md存在且覆盖环境搭建/模块开发顺序/关键技术点
- [x] 开发思路说明文档.md存在且覆盖架构设计/网络同步/角色控制/技术选型/问题说明

## 可运行验证
- [x] 结构性验证：所有asmdef/源码/proto/配置/文档文件存在且内容正确
- [x] manifest.json验证：以file:方式引用21个ET包+3个本地业务包
- [x] 关键代码验证：LSInput含V/Jump/SkillId/SkillDir，ET.Hotfix的noEngineReferences=true
- [ ] dotnet build ET.sln编译通过（需在Unity中初始化后执行）
- [ ] 服务端可通过dotnet Bin/ET.App.dll --Console=1启动（需编译后执行）
- [ ] 客户端可在Unity编辑器Play运行（需Unity打开后执行）
- [ ] 2个客户端可匹配进入同一房间（需运行时验证）
- [ ] 一个客户端角色移动在另一个客户端可见且状态一致（需运行时验证）
