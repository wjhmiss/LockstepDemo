# ET10 包依赖关系分析文档

> 基于 `d:\Unity\LockstepDemo\ET` 项目所有包的 `package.json` 真实依赖数据整理
> 数据来源：每个 `cn.etetet.*` 包下的 `package.json` 文件

---

## 一、依赖规则

根据 [cn.etetet.harness/AGENTS.md](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.harness/AGENTS.md) 中定义的规范：

1. **单向依赖**：包之间只能单向依赖，A依赖B则B永远不能依赖A
2. **显式声明**：跨包访问必须在 `package.json` 显式声明依赖
3. **递归传递**：依赖需要递归传递，修改依赖时要把依赖的依赖全部加上
4. **层级约束**：通常只能高层包依赖低层包，不存在同层互访例外

---

## 二、包依赖完整清单

### 第0层 - 基础设施包（无ET依赖）

| 包名 | 版本 | 功能 | 依赖 |
|------|------|------|------|
| cn.etetet.sourcegenerator | 0.0.5 | 源码生成器 | 无ET依赖 |
| cn.etetet.memorypack | 1.10.1 | MemoryPack序列化 | 无ET依赖 |
| cn.etetet.mathematics | 1.0.0 | Unity数学库 | 无ET依赖 |
| cn.etetet.truesync | 1.0.0 | 真同步库 | 无ET依赖 |
| cn.etetet.conditionexpr | 1.0.0 | 条件表达式 | 无ET依赖 |
| cn.etetet.yiuiframework | 4.0.0 | YIUI框架基础 | 无ET依赖 |
| cn.etetet.yiuieffect | 1.0.0 | YIUI特效 | 无ET依赖 |
| cn.etetet.yiuigm | 1.0.0 | YIUI GM | 无ET依赖 |
| cn.etetet.yiuiinvoke | 1.0.0 | YIUI调用 | 无ET依赖 |
| cn.etetet.yiuiluban | 1.0.0 | YIUI Luban集成 | 无ET依赖 |
| cn.etetet.yiuireddot | 1.0.0 | YIUI红点 | 无ET依赖 |
| cn.etetet.yiuitips | 1.0.0 | YIUI提示 | 无ET依赖 |
| cn.etetet.yiui3ddisplay | 1.0.0 | YIUI 3D展示 | 无ET依赖 |
| cn.etetet.yiuiyooassets | 1.0.0 | YIUI YooAssets集成 | 无ET依赖 |
| cn.etetet.yiuiloopscrollrectasync | 1.0.0 | YIUI循环列表 | 无ET依赖 |
| com.etetet.init | 1.0.0 | 项目初始化 | 无ET依赖 |

### 第1层 - 核心基础包

| 包名 | 版本 | 功能 | 依赖 |
|------|------|------|------|
| cn.etetet.core | 4.0.0 | 核心框架 | sourcegenerator, memorypack |
| cn.etetet.proto | 4.0.0 | 协议定义 | core |
| cn.etetet.loader | 3.0.1 | 加载器 | core |
| cn.etetet.config | 4.0.0 | Luban配置系统 | core |
| cn.etetet.db | 4.0.0 | 数据库 | core |
| cn.etetet.console | 3.0.0 | 控制台 | core |
| cn.etetet.startconfig | 4.0.0 | 服务器启动配置 | core |
| cn.etetet.hybridclr | 7.8.1 | 热更新 | core |
| cn.etetet.lsentity | 3.0.0 | 帧同步实体 | core |
| cn.etetet.yooassets | 2.3.6 | YooAsset资源加载 | core |
| cn.etetet.yiui | 4.0.0 | YIUI核心 | core |
| cn.etetet.unitybridge | 2.0.0 | Unity Bridge | core |
| cn.etetet.recast | 2.0.0 | Recast寻路 | core |
| cn.etetet.http | 4.0.0 | HTTP服务 | core |
| cn.etetet.behaviortree | 4.0.0 | 行为树 | core |
| cn.etetet.btnode | 4.0.0 | 行为树节点 | core |
| cn.etetet.harness | 1.0.0 | AI Harness | core, config |
| cn.etetet.equipment | 4.0.0 | 装备系统 | core, config |
| cn.etetet.achievement | 1.0.0 | 成就系统 | core, config |
| cn.etetet.memorypack | 1.10.1 | MemoryPack桥接 | core |

### 第2层 - 功能基础包

| 包名 | 版本 | 功能 | 依赖 |
|------|------|------|------|
| cn.etetet.unit | 2.0.0 | 单位实体 | core, config, proto |
| cn.etetet.netinner | 2.0.1 | 内网消息 | core, config, loader, proto, startconfig, sourcegenerator |

### 第3层 - 业务基础包

| 包名 | 版本 | 功能 | 依赖 |
|------|------|------|------|
| cn.etetet.numeric | 2.0.0 | 数值系统 | core, config, proto, unit |
| cn.etetet.router | 2.0.1 | 软路由 | core, config, loader, netinner, proto, servicediscovery, startconfig, http |
| cn.etetet.servicediscovery | 4.0.0 | 服务发现 | core, startconfig, netinner |

### 第4层 - 高级功能包

| 包名 | 版本 | 功能 | 依赖 |
|------|------|------|------|
| cn.etetet.actorlocation | 2.0.1 | Actor Location | core, db, config, proto, netinner, servicediscovery, startconfig, test, loader |
| cn.etetet.aoi | 4.0.0 | 九宫格AOI | core, config, proto, unit, numeric |
| cn.etetet.map | 1.0.0 | 地图基础 | actorlocation, aoi, config, core, loader, netinner, numeric, proto, recast, servicediscovery, unit, login |

### 第5层 - 玩法系统包

| 包名 | 版本 | 功能 | 依赖 |
|------|------|------|------|
| cn.etetet.move | 2.0.0 | 移动系统 | core, login, map, numeric, proto, recast, unit |
| cn.etetet.item | 4.0.0 | 道具背包 | core, config, map, proto, unit |
| cn.etetet.spell | 4.0.0 | 技能Buff系统 | core, config, proto, unit, behaviortree, http, startconfig, console, numeric, map, netinner, router, actorlocation, aoi, login, yooassets, yiuiframework |
| cn.etetet.quest | 4.0.0 | 任务系统 | config, login, map, proto, spell, unit, core |

### 第6层 - 地图玩法包

| 包名 | 版本 | 功能 | 依赖 |
|------|------|------|------|
| cn.etetet.mapplay | 1.0.0 | 地图玩法层 | actorlocation, aoi, config, core, item, loader, login, map, move, netinner, numeric, proto, quest, recast, servicediscovery, spell, unit, yooassets, yiuiframework, yiuitips |
| cn.etetet.login | 2.0.1 | 登录系统 | actorlocation, loader, netinner, proto, servicediscovery, core |

### 第7层 - 传送业务包

| 包名 | 版本 | 功能 | 依赖 |
|------|------|------|------|
| cn.etetet.transfer | 1.0.0 | 传送系统 | actorlocation, aoi, config, core, item, loader, login, map, mapplay, move, netinner, numeric, proto, quest, recast, servicediscovery, spell, unit |

### 第8层 - 机器人/测试/游戏入口包

| 包名 | 版本 | 功能 | 依赖 |
|------|------|------|------|
| cn.etetet.robot | 2.0.0 | 机器人 | console, loader, login, map, mapplay, proto, router, core |
| cn.etetet.test | 2.0.0 | 测试系统 | loader, map, mapplay, router, unitybridge |
| cn.etetet.statesync | 4.0.0 | 状态同步(WOW) | core, config, proto, unit, behaviortree, http, startconfig, console, numeric, move, recast, netinner, router, actorlocation, aoi, yiuiframework, yooassets, yiuitips, yiuigm, loader, yiuiyooassets, yiuiluban, test, map, mapplay, transfer, servicediscovery, yiuiloopscrollrectasync, login, btnode, quest, item, mapmanager, yiuireddot, spell, robot, sourcegenerator, yiuiinvoke, db, unitybridge, conditionexpr |
| cn.etetet.lockstep | 3.0.2 | 帧同步 | packagemanager, actorlocation, core, loader, router, console, http, truesync, lsentity, login, netinner, hybridclr, config, proto, sourcegenerator, yooassets, memorypack, startconfig, yiuiframework, yiuitips, yiuigm, yiuiyooassets, yiuiluban, test, servicediscovery, yiuiloopscrollrectasync, yiuireddot |
| cn.etetet.mapmanager | 1.0.0 | 地图管理 | core, config |

---

## 三、依赖层级图

```
┌─────────────────────────────────────────────────────────────────┐
│  第8层 - 游戏入口                                                │
│  statesync ─ lockstep ─ robot ─ test                            │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│  第7层 - 传送业务                                                │
│  transfer                                                        │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│  第6层 - 地图玩法 / 登录                                         │
│  mapplay ─ login                                                 │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│  第5层 - 玩法系统                                                │
│  move ─ item ─ spell ─ quest                                     │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│  第4层 - 高级功能                                                │
│  actorlocation ─ aoi ─ map                                       │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│  第3层 - 业务基础                                                │
│  numeric ─ router ─ servicediscovery                             │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│  第2层 - 功能基础                                                │
│  unit ─ netinner                                                 │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│  第1层 - 核心基础                                                │
│  core ─ proto ─ config ─ loader ─ db ─ console ─ startconfig    │
│  http ─ recast ─ yooassets ─ yiui ─ hybridclr ─ unitybridge     │
│  behaviortree ─ lsentity ─ ...                                   │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│  第0层 - 基础设施                                                │
│  sourcegenerator ─ memorypack ─ mathematics ─ truesync           │
│  conditionexpr ─ yiuiframework ─ yiuieffect ─ ...                │
└─────────────────────────────────────────────────────────────────┘
```

---

## 四、核心依赖链路分析

### 4.1 状态同步游戏入口链路（statesync）

这是本项目最完整的依赖链路，statesync几乎依赖了所有包：

```
statesync
├── transfer ─── mapplay ─── spell ─── map ─── actorlocation ─── netinner ─── core
│                │            │         │         │                  │
│                │            │         │         ├── db             ├── config
│                │            │         │         ├── servicediscovery ├── loader
│                │            │         │         └── startconfig    ├── proto
│                │            │         │                            └── startconfig
│                │            │         ├── aoi ─── numeric ─── unit ─── config
│                │            │         │                └── core
│                │            │         ├── numeric
│                │            │         ├── unit
│                │            │         └── login ─── actorlocation
│                │            │
│                │            ├── behaviortree ─── core
│                │            ├── numeric
│                │            ├── aoi
│                │            └── login
│                │
│                ├── item ─── map ─── unit ─── config ─── core
│                ├── move ─── map ─── recast ─── core
│                ├── quest ─── spell ─── login
│                └── login
│
├── robot ─── mapplay ─── console ─── core
│             └── router
│
├── test ─── mapplay ─── unitybridge ─── core
│            └── router
│
└── (所有UI/YIUI相关包)
```

### 4.2 帧同步游戏入口链路（lockstep）

```
lockstep
├── actorlocation ─── netinner ─── core
├── login ─── actorlocation
├── router ─── netinner ─── http ─── core
├── lsentity ─── core
├── truesync (无依赖)
├── console ─── core
├── hybridclr ─── core
├── config ─── core
├── proto ─── core
├── yooassets ─── core
└── (YIUI UI包群)
```

### 4.3 从core到map的典型依赖路径

```
core (第1层)
  └── config, proto, loader (第1层)
       └── unit (第2层)
            └── numeric (第3层)
                 └── aoi (第4层)
                      └── map (第4层)
```

---

## 五、被依赖次数统计

按被其他包直接依赖的次数排序（不含自身）：

| 排名 | 包名 | 被依赖次数 | 说明 |
|------|------|-----------|------|
| 1 | cn.etetet.core | 30+ | 所有包的根基 |
| 2 | cn.etetet.proto | 20+ | 协议定义，几乎必选 |
| 3 | cn.etetet.config | 18+ | 配置系统，几乎必选 |
| 4 | cn.etetet.loader | 10+ | 资源加载 |
| 5 | cn.etetet.map | 8+ | 地图基础，被玩法包广泛依赖 |
| 6 | cn.etetet.unit | 8+ | 单位实体 |
| 7 | cn.etetet.netinner | 8+ | 内网通信 |
| 8 | cn.etetet.login | 7+ | 登录系统 |
| 9 | cn.etetet.actorlocation | 6+ | Actor定位 |
| 10 | cn.etetet.servicediscovery | 6+ | 服务发现 |
| 11 | cn.etetet.numeric | 5+ | 数值系统 |
| 12 | cn.etetet.spell | 4+ | 技能系统 |
| 13 | cn.etetet.startconfig | 4+ | 启动配置 |
| 14 | cn.etetet.aoi | 4+ | AOI系统 |

---

## 六、包间依赖约束规则（来自各包AGENTS.md）

### 6.1 map包约束

> 地图基础包。不能反向依赖 `move`。不要依赖 `mapplay`、`spell`、`item`、`quest` 等更高层玩法包。

```
map → 不依赖 → move, mapplay, spell, item, quest
move → 可以依赖 → map
```

### 6.2 move包约束

> move依赖map，map不反向依赖move，避免包循环。不要依赖mapplay、spell、item、quest等更高层玩法包。

### 6.3 mapplay包约束

> 依赖map基础包。可以依赖spell、item、quest、login、move等玩法包。不允许被map反向依赖。

### 6.4 transfer包约束

> 上层传送业务包，可以依赖mapplay。robot、test、statesync等需要传送能力的包应显式依赖本包。

### 6.5 actorlocation包约束

> Location路由状态会持久化到DB。Location锁必须使用token闭环。

---

## 七、依赖传递图（以statesync为例）

以下展示statesync包的完整传递依赖（递归展开）：

```
cn.etetet.statesync
│
├── cn.etetet.transfer
│   ├── cn.etetet.mapplay
│   │   ├── cn.etetet.map
│   │   │   ├── cn.etetet.actorlocation
│   │   │   │   ├── cn.etetet.netinner
│   │   │   │   │   ├── cn.etetet.startconfig
│   │   │   │   │   └── cn.etetet.proto
│   │   │   │   └── cn.etetet.db
│   │   │   ├── cn.etetet.aoi
│   │   │   │   ├── cn.etetet.numeric
│   │   │   │   │   └── cn.etetet.unit
│   │   │   │   │       └── cn.etetet.config
│   │   │   │   └── cn.etetet.unit
│   │   │   └── cn.etetet.recast
│   │   ├── cn.etetet.move
│   │   ├── cn.etetet.spell
│   │   ├── cn.etetet.item
│   │   └── cn.etetet.quest
│   └── cn.etetet.login
│
├── cn.etetet.robot
│   └── cn.etetet.mapplay (→ 已展开)
│
├── cn.etetet.test
│   └── cn.etetet.mapplay (→ 已展开)
│
├── cn.etetet.router
│   └── cn.etetet.netinner (→ 已展开)
│
└── (UI/YIUI包群)
    ├── cn.etetet.yiuiframework
    ├── cn.etetet.yiuitips
    ├── cn.etetet.yiuigm
    ├── cn.etetet.yiuiyooassets
    ├── cn.etetet.yiuiluban
    └── cn.etetet.yiuireddot
```

---

## 八、关键设计模式

### 8.1 核心下沉模式

`cn.etetet.core` 作为最底层包，承载了所有框架基础设施：

```
core提供的能力:
├── Entity/Component/IScene    → 被所有包使用
├── EventSystem/Invoke         → 被所有包使用
├── ETTask/ETVoid              → 被所有包使用
├── Fiber/FiberManager         → 被需要多线程的包使用
├── TimerComponent             → 被需要定时器的包使用
├── Network (KCP/TCP/WS)       → 被netinner/login等网络包使用
├── ObjectPool                 → 被所有包使用
├── Serialize (MemoryPack/Bson)→ 被所有包使用
└── Singleton/World            → 被所有包使用
```

### 8.2 纵向分层模式

同一功能领域按抽象程度纵向分层：

```
地图领域:
map(基础) → mapplay(玩法层) → transfer(传送业务)

数值领域:
unit(实体) → numeric(数值) → aoi(九宫格) → spell(技能)

网络领域:
core(网络基础) → netinner(内网) → actorlocation(定位) → login(登录)
```

### 8.3 横向隔离模式

同层包之间互不依赖，通过上层包组装：

```
第5层玩法包之间互不直接依赖:
move ← 不依赖 → item ← 不依赖 → spell ← 不依赖 → quest

由上层mapplay/transfer统一组装:
mapplay → move + item + spell + quest
transfer → mapplay + move + item + spell + quest
```

---

## 九、新增包的依赖检查清单

开发新包时，请按以下步骤确认依赖：

1. **确定层级**：新包属于哪一层？只能依赖同级或更低层的包
2. **最小依赖**：只写源码直接引用的包，不要递归展开间接依赖
3. **避免循环**：确保没有A→B→A的循环依赖
4. **显式声明**：所有跨包访问必须在package.json声明
5. **递归补全**：修改依赖后，把依赖的依赖也递归加上
6. **验证编译**：使用 `dotnet build ET.sln` 验证

---

## 十、源文件索引

| 包名 | package.json路径 |
|------|-----------------|
| cn.etetet.core | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.core/package.json) |
| cn.etetet.unit | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.unit/package.json) |
| cn.etetet.numeric | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.numeric/package.json) |
| cn.etetet.netinner | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.netinner/package.json) |
| cn.etetet.actorlocation | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.actorlocation/package.json) |
| cn.etetet.aoi | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.aoi/package.json) |
| cn.etetet.map | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.map/package.json) |
| cn.etetet.move | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.move/package.json) |
| cn.etetet.item | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.item/package.json) |
| cn.etetet.spell | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.spell/package.json) |
| cn.etetet.quest | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.quest/package.json) |
| cn.etetet.mapplay | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.mapplay/package.json) |
| cn.etetet.login | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.login/package.json) |
| cn.etetet.transfer | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.transfer/package.json) |
| cn.etetet.router | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.router/package.json) |
| cn.etetet.robot | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.robot/package.json) |
| cn.etetet.test | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.test/package.json) |
| cn.etetet.statesync | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.statesync/package.json) |
| cn.etetet.lockstep | [package.json](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.lockstep/package.json) |
| cn.etetet.harness | [AGENTS.md](file:///d:/Unity/LockstepDemo/ET/Packages/cn.etetet.harness/AGENTS.md) (层级定义来源) |
