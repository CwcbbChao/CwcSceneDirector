# CwcSceneDirector - 场景内容生成与动态刷怪导演系统

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Custom%20(Free%20for%20Games)-blue.svg)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/CwcbbChao/CwcSceneDirector/pulls)

[English](README_EN.md) | **简体中文**

---

## 插件简介

`CwcSceneDirector` 是一个专为 Unity 研发的**高性能、模块化、纯 C# 驱动**的场景内容摆放与 AI 动态刷怪导演系统。

系统深度融合了**《暗黑破坏神 4》（Diablo IV）**的大地图两级选点与洗牌配额算法，以及**《雨中冒险 2》（Risk of Rain 2）**的 AI 信用点预算与波次调度机制。它将宏观空间选点、微观战术阵型、物理环境校验、实体对象池与超距自动淘汰高度内聚为统一的调度管线，专为肉鸽地牢、开放世界遭遇战、地下城宝箱生成及世界狂潮事件提供工业级解决方案。

核心框架完全由纯 C# 类与接口驱动，**无任何外部项目业务依赖**，支持按需懒创建与零 GC 运行。

---

## 核心设计特性

### 1. 暗黑 4 两级选点与洗牌配额牌堆（Quota Deck）
- **宏观与微观两级分离**：先在宏观区域内采集均匀候选群落中心，再在微观散开半径内根据阵型和物理底盘展开小队，层次清晰。
- **配额牌堆算法**：
  - **第一阶段（保底）**：优先满足各条目的 `MinLimit` 强制保底名额；
  - **第二阶段（权重）**：剩余名额按 `Weight` 权重分配给未达 `MaxLimit` 上限的条目；
  - **第三阶段（洗牌）**：采用 Fisher-Yates 随机打乱发牌。既保证宏观产出比绝对受控，又具备不可预测的自然探索感。

### 2. 雨中冒险 2 动态信用点刷怪机制（Credit Director）
- **目标锁定防饥饿机制（Target Lock）**：当骰选出高费精英怪（High Cost）而同屏容量暂不可用时，系统会自动锁定该目标并等待玩家清怪释放战力容量，彻底杜绝高费大怪因刷新窗口狭窄而永远无法出场的“饥饿问题”。
- **软上限与负蓝透支（Soft Cap Overdraft）**：剩余总预算大于 0 时，允许整队买下并透支到负数，避免关卡尾声残留微量预算导致怪群卡死。
- **实收单向审计（Accurate Budget Consumption）**：不提前预扣预算，各单位出池激活时由实体管理器按实际生成的真实 `ThreatCost` 自动扣费，实生多少扣多少。
- **波次呼吸冷却（Wave Cooldown）**：每波生成后引入生理冷却时间，结合同屏在场容量上限 `MaxConcurrentCost`，形成张弛有度的战斗心流。

### 3. 开阔腹地加权最远点采样（Spacious-FPS）
- 结合**地面点云局部饱满度（代表腹地面积）**与**空间最远点离散度（Farthest Point Sampling）**，自动优先挑选各大房间开阔腹地正中心。
- 天然跨房间均衡扩散，杜绝大房间不刷、小走廊扎堆卡位的业界通病；全程纯点云拓扑分析，零物理射线开销。

### 4. 葵花黄金角螺旋点阵与弹性回弹（Sunflower / Vogel Spiral）
- 微观小队生成采用黄金角（$137.5^\circ$）葵花螺旋点阵（Vogel Spiral），大怪稳居中心（$r = 0$），小怪等比向外旋绕展开，全阵列面密度处处均等。
- **弹性向心收缩（Inward Bounce）**：外圈点遇到墙体或悬崖时，自动向中心向量回缩收紧。
- **满额保底补齐（No Monster Left Behind）**：若极端地形依然受阻，在散开半径内随机采样平地补足名额，严格落实绝不吞怪原则。

### 5. 原生物理底盘自适应与悬崖防空下沉探针
- **原生物理底盘提取**：自动从原生 `Collider`、`CharacterController` 或 `Renderer` 包围盒反推底盘占用半径 $R$，预制体无需挂载任何标记组件或实现接口。
- **悬崖防空下沉探针（`IsLedgeSafe`）**：以半径 $R$ 向四个正交方向发射下沉探针，若悬空入虚空或台阶落差过大直接否决候选点，彻底根治怪物刷新在悬崖半空的浮空穿模问题。
- **靠墙相切吸附（`TrySnapToWall`）**：以自身半径 $R$ 自动推开严丝合缝贴墙，并自动调整背墙朝向面向室内开阔腹地。

### 6. 高性能 2D 空间哈希网格与 2.5D 世界分块点云
- **2D 空间哈希网格（`CwcSpatialGrid`）**：以 X-Z 水平面进行哈希网格分区，维护对象池化列表，实现区域实体与累计战力开销的近似 $O(1)$ 复杂度零 GC 查询。
- **2.5D 世界分块（`CwcSpatialChunk`）**：按需懒加载探测场景地貌与可行走区域，支持局部区域主动失效与动态障碍物实时更新。

### 7. 分帧时间预算调度（Time-Sliced Frame Budgeting）
- 运行时上下文（`CwcPlacementContext`）内建高精度 `Stopwatch`，单帧耗时超出性能预算（默认 2.0ms）时才主动出让控制权（`yield return null`），时间充裕时同帧批量生成，兼具极速生成与绝不掉帧。

### 8. 集中化对象池与超距自动淘汰回收（Cull & Despawn）
- 超出关注目标（玩家）判定距离后自动触发实体 `Despawn()` 归池，自带新出池保护缓冲期（`_cullGracePeriod`）与 Boss 豁免标记（`AllowCulling = false`）。
- 场景 Hierarchy 树按预制体名分类收纳，井井有条。

### 9. 现代卡片式 PropertyDrawer 与运行时监控面板
- 提供紧凑美观的卡片式自定义属性绘制器，根据类型智能匹配强调色（交互物为天蓝，信用点敌群为红橙，通用为蓝紫），支持内联权重编辑与脚本溯源跳转。
- 提供运行时实时监视面板，动态查看各模块运行状态（Running / Paused / Completed）与优先级，支持一键调试干预。

---

## 安装方式

### 方式 A：通过 Unity Package Manager (Git URL 推荐)
1. 打开 Unity 编辑器菜单栏：`Window` -> `Package Manager`。
2. 点击左上角 `+` 号 -> 选择 **Add package from git URL...**。
3. 输入仓库地址：
   ```text
   https://github.com/CwcbbChao/CwcSceneDirector.git
   ```
4. 点击 **Add** 即可完成自动安装。

### 方式 B：源码直接导入
将 `CwcSceneDirector` 文件夹直接放入项目的 `Assets/` 或 `Packages/` 目录下即可。

---

## 快速上手 (Quick Start)

### 1. 静态交互物 / 宝箱加权洗牌摆放
```csharp
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

public class DungeonChestSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _woodenChestPrefab;
    [SerializeField] private GameObject _goldChestPrefab;
    [SerializeField] private GameObject _shrinePrefab;

    private void Start()
    {
        // 创建一次性摆放模块：以当前位置为中心，半径 40m，最多摆放 12 个群落，间距至少 8m
        var placementModule = new CwcWeightedPlacementModule(
            center: transform.position,
            radius: 40f,
            minClusterDistance: 8f,
            maxClusters: 12
        );

        // 添加普通宝箱：权重 70，靠墙放置，每点 1 个
        placementModule.AddItem(new CwcInteractablePlacementItem(_woodenChestPrefab, weight: 70, mode: PlacementMode.WallSnapped));

        // 添加黄金宝箱：权重 20，靠墙放置，每点 1 个，最多允许 2 个
        var goldChestItem = new CwcInteractablePlacementItem(_goldChestPrefab, weight: 20, mode: PlacementMode.WallSnapped);
        goldChestItem.MaxLimit = 2;
        placementModule.AddItem(goldChestItem);

        // 添加开阔地神龛：权重 10，开阔地放置，保底 1 个
        var shrineItem = new CwcInteractablePlacementItem(_shrinePrefab, weight: 10, mode: PlacementMode.OpenCenter);
        shrineItem.MinLimit = 1;
        placementModule.AddItem(shrineItem);

        // 注册到场景总导演执行调度（摆放完毕后模块将自动销毁退出）
        CwcSceneDirector.Register(placementModule);
    }
}
```

### 2. 动态敌群狂潮 / 遭遇战调度
```csharp
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

public class ArenaEncounterSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _minionPrefab;
    [SerializeField] private GameObject _elitePrefab;
    [SerializeField] private Transform _playerTransform;

    private CwcCreditDirectorModule _encounterModule;

    public void StartEncounter()
    {
        // 创建信用点导演：跟随玩家位置，外环 30m，内环安全距离 8m，总预算 60 点，同屏在场战力上限 15 点
        _encounterModule = new CwcCreditDirectorModule(
            center: _playerTransform.position,
            radius: 30f,
            totalBudget: 60,
            maxConcurrentCost: 15,
            minWaveCost: 2,
            minRadius: 8f,
            centerTarget: _playerTransform
        );

        // 设置波次呼吸冷却为 3.0 秒
        _encounterModule.SetWaveCooldown(3.0f);

        // 普通小怪：单只消耗 1 点，权重 80，每队 4 只（小队总消耗 4 点）
        _encounterModule.AddItem(new CwcCreditPrefabPlacementItem(_minionPrefab, cost: 4, weight: 80, count: 4));

        // 强力精英：单只消耗 5 点，权重 20，每队 1 只（小队总消耗 5 点），开阔地生成
        _encounterModule.AddItem(new CwcCreditPrefabPlacementItem(_elitePrefab, cost: 5, weight: 20, count: 1, mode: PlacementMode.OpenCenter));

        // 事件监听：总预算耗尽与清场结算
        _encounterModule.OnBudgetDepleted += () => Debug.Log("警告：敌群援军已断绝！");
        _encounterModule.OnClearedAndCompleted += () => Debug.Log("恭喜！所有敌人肃清完毕，遭遇战通关！");

        // 注册并激活导演
        CwcSceneDirector.Register(_encounterModule);
    }

    public void StopEncounter()
    {
        if (_encounterModule != null)
        {
            CwcSceneDirector.Unregister(_encounterModule);
            _encounterModule = null;
        }
    }
}
```

---

## 演示示例体验 (Demo Scene)

- **场景路径**：`Assets/CwcPlugins/CwcSceneDirector/Demo/Demo_SceneDirector.unity`。
- **开箱即用**：导入插件后可直接双击打开该场景体验，无需额外配置或解压。
- **键盘操作热键**：
  - **I**：一键在全图地牢六大区域生成分布均匀的交互物与宝箱；
  - **C**：一键清除当前场景中的所有宝箱；
  - **E**：激活敌群遭遇战（暗黑狂潮与雨险 AI Director 联动）；
  - **K**：强制中止当前遭遇战；
  - **X**：一键模拟清场（击杀当前场上所有敌人）。
- **完全解耦**：Demo 模块包含独立的程序集定义（`.asmdef`），核心 `Runtime` 与 `Editor` 模块对 Demo 零反向依赖。

---

## 核心架构与类职责表

| 类名 | 职责定位 | 说明 |
| :--- | :--- | :--- |
| `CwcSceneDirector` | 场景总导演核心调度器 | MonoBehaviour 驱动，支持懒创建、运行时优先级排序、全局与单模块暂停/恢复 |
| `CwcSceneDirectorModule` | 导演模块抽象基类 | 纯 C# 类逻辑载体，声明生命周期与协同协程支持 |
| `CwcWeightedPlacementModule` | 一次性加权摆放模块 | 暗黑 4 配额牌堆洗牌算法，支持保底、权重分配与上限封顶，摆放完毕后自销毁 |
| `CwcCreditDirectorModule` | 动态信用点导演模块 | 雨中冒险 2 遭遇战机制，支持目标锁定防饥饿、负蓝透支、单向实收审计与波次冷却 |
| `CwcSceneEntityManager` | 场景实体对象池与超距管理器 | 集中管理对象池分类层级、超距自动淘汰回收、关注目标追踪与总威胁度统计 |
| `CwcSpatialGrid` | 2D 空间哈希网格 | 基于 X-Z 平面的空间哈希分区，提供近似 $O(1)$ 复杂度的实体查询与战力汇总，零 GC |
| `CwcSceneSpatialManager` | 2.5D 世界分块点云空间感知器 | 分块懒探测地表地貌，提供开阔腹地加权最远点采样（Spacious-FPS）与局部失效刷新 |
| `CwcPlacementSpatialUtil` | 空间选点与物理几何工具库 | 泊松盘、葵花黄金角螺旋点阵、底盘自适应、悬崖防空探针与靠墙相切算法库 |
| `IPlacementStrategy` | 摆放物理策略多态契约 | 策略模式接口，内置 `Free`（自由）、`WallSnapped`（靠墙）与 `OpenCenter`（开阔地） |
| `CwcPlacementContext` | 运行时执行上下文 | 携带总调度宿主与物理配置，提供单帧时间预算监控（`ShouldYield`）防掉帧 |

---

## 许可协议与商用授权

- 本项目核心源码采用 [Cwc Tools Public License (Source-Available)](LICENSE) 许可：
  - **商业游戏发布（End Products）**：允许个人及商业游戏项目免费集成使用并发布商用，免收任何版税（Royalty-Free）。
  - **二次分发限制（No Redistribution as Tools）**：严禁以任何形式将本插件本体或修改版本作为独立开发工具、SDK、资产包或竞品插件进行二次分发、公开镜像或转售。
- 完整第三方声明详见 [Third-Party Notices.txt](Third-Party%20Notices.txt)。
