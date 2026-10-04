# CwcSceneDirector - 场景内容摆放与动态刷怪导演系统

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Custom%20(Free%20for%20Games)-blue.svg)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/CwcbbChao/CwcSceneDirector/pulls)

[English](README_EN.md) | **简体中文**

---

## 它是用来做什么的？

在制作地牢、肉鸽、开放世界或关卡游戏时，我们经常需要：
1. **在场景里摆东西**：随机撒宝箱、神龛、资源矿石或陷阱，要求**分布均匀、不扎堆、比例受控、靠墙对齐、不刷在悬崖外**。
2. **在战斗中刷怪**：像《雨中冒险2》或《暗黑破坏神4》一样，根据战斗节奏动态刷出小怪与精英怪，要求**大怪带小怪有阵型、玩家打得慢就暂缓出怪、玩家杀得快就快速补充、大批量出怪绝不掉帧**。

`CwcSceneDirector` 就是为了解决这些痛点而生的 Unity 开源插件。它是一个**纯 C# 驱动、轻量高性能、开箱即用**的场景生成调度系统。

---

## 核心功能解决的痛点

### 1. 摆宝箱与场景物件：按比例洗牌，绝不扎堆
- **按比例洗牌发牌**：例如设置“木宝箱 70%、金宝箱 20%、神秘神龛 10%”，并且可以强制指定“神龛至少保底 1 个，金宝箱全图最多 2 个”。系统像发牌一样把名额分配好再随机打乱，绝不会出现全图全都是金宝箱或全图没有神龛的极端运气情况。
- **智能避让与靠墙对齐**：
  - **自由模式（Free）**：平整贴地放置，物体之间保持安全间距。
  - **贴墙模式（WallSnapped）**：自动寻找附近的墙壁，严丝合缝靠墙摆好，并且**自动背靠墙壁、面向室内开阔区域**。
  - **开阔地模式（OpenCenter）**：自动寻找房间最空旷平坦的腹地正中心摆放。

### 2. 动态刷怪与遭遇战：张弛有度，拒绝无脑堆怪
- **战力预算与在场上限控制**：给导演一个总预算（例如本场遭遇战总战力 60 点），以及同屏在场战力上限（例如同屏最多 15 点）。
- **呼吸感战斗节奏**：玩家杀怪慢、压力大时，场上战力超标，导演会自动停止刷新，给玩家喘息时间；每波怪物生成后有“呼吸冷却期”，不会像水龙头漏水一样连续不断地贴脸出怪。
- **大怪防饥饿机制**：如果抽中了高费大怪或精英小队，但当前同屏剩余容量不够，导演会**锁定该大怪排队等待**，等玩家击杀小怪腾出空间后立即放行，绝不会因为小怪便宜而把大怪永远挤掉。
- **预算透支机制**：战斗快结束时，哪怕只剩 2 点预算，只要抽中了一队 5 点的怪群，导演也会允许直接“借款买下”整队刷出，杜绝尾声残留微小预算导致卡住不刷怪。
- **不贴脸刷新**：支持内环安全距离，怪物绝不会直接骑脸刷在玩家脚底。

### 3. 自然好看的怪群阵型：大怪居中，小怪环绕
- **向日葵螺旋阵型**：一队怪刷出时，精英大怪稳居中心，随从随行小怪像向日葵花瓣一样均匀向外展开，全队疏密一致，绝不叠成一个点。
- **撞墙自动往中间缩（弹性回弹）**：如果阵型外圈撞到了墙壁或障碍物，小怪会自动沿半径向中心聚拢收紧。
- **绝不吞怪保底**：如果极端地形导致某个小怪放不下，系统会在散开范围内自动重新寻找平坦地面补足名额，严格保证配置几只就刷出几只。

### 4. 物理底盘自适应与悬崖防空：踩不到实地绝不刷
- **免挂脚本，自动识别大小**：无需在预制体上挂任何特定脚本，系统自动根据怪物或宝箱自身的碰撞盒（Collider）、角色控制器（CharacterController）或网格大小，算出它的占地半径。
- **四角悬空探测**：生成前自动向物体四周发射下沉探针，如果脚底踩空（悬浮在悬崖深渊之上）或者台阶高低落差过大，直接放弃该点，彻底根治怪物刷新在悬崖半空中卡模的 Bug。

### 5. 分帧时间预算：连续刷几十只怪也绝不掉帧
- 传统的 Instantiate 如果在同一帧生成几十只怪物，游戏会产生明显的顿卡。
- 本插件自带分帧时间预算控制（默认单帧预算 2.0 毫秒）。当帧时间充裕时，极速同帧连出；单帧时间耗尽时，自动暂停并顺延到下一帧继续出怪，全过程丝滑流畅。

### 6. 内置对象池与超距自动回收
- 怪物远离玩家超出设定距离（例如 60 米）后，会自动回收归池，不需要开发者手写对象池代码。
- 自带出生保护期（新刷出的怪不会瞬间被回收）以及 Boss 豁免标记（关键敌人永久不被回收）。

---

## 安装方式

### 方式 A：通过 Unity Package Manager (推荐)
1. 打开 Unity 编辑器菜单栏：`Window` -> `Package Manager`。
2. 点击左上角 `+` 号 -> 选择 **Add package from git URL...**。
3. 填入仓库地址：
   ```text
   https://github.com/CwcbbChao/CwcSceneDirector.git
   ```
4. 点击 **Add** 等待安装完成。

### 方式 B：直接复制代码
将 `CwcSceneDirector` 文件夹直接放入项目的 `Assets/` 或 `Packages/` 目录下即可。

---

## 快速上手

### 场景一：在地下城里摆宝箱和神龛（一次性加权摆放）

```csharp
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

public class ChestSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _woodenChestPrefab; // 普通木宝箱
    [SerializeField] private GameObject _goldChestPrefab;   // 黄金大宝箱
    [SerializeField] private GameObject _shrinePrefab;      // 增益神龛

    private void Start()
    {
        // 1. 创建摆放模块：以当前位置为中心，半径 35 米内，最多摆放 10 个点，每个点之间至少隔 7 米
        var placement = new CwcWeightedPlacementModule(
            center: transform.position,
            radius: 35f,
            minClusterDistance: 7f,
            maxClusters: 10
        );

        // 2. 添加普通木宝箱：权重 70，自动贴墙摆放
        placement.AddItem(new CwcInteractablePlacementItem(_woodenChestPrefab, weight: 70, mode: PlacementMode.WallSnapped));

        // 3. 添加黄金大宝箱：权重 20，贴墙摆放，全图最多只允许出 2 个
        var goldChest = new CwcInteractablePlacementItem(_goldChestPrefab, weight: 20, mode: PlacementMode.WallSnapped);
        goldChest.MaxLimit = 2;
        placement.AddItem(goldChest);

        // 4. 添加增益神龛：权重 10，放在开阔地中心，全图至少保底出 1 个
        var shrine = new CwcInteractablePlacementItem(_shrinePrefab, weight: 10, mode: PlacementMode.OpenCenter);
        shrine.MinLimit = 1;
        placement.AddItem(shrine);

        // 5. 注册到导演执行！（生成完毕后该模块会自动销毁，不占后续运行资源）
        CwcSceneDirector.Register(placement);
    }
}
```

---

### 场景二：做一场像《雨中冒险》一样的刷怪遭遇战

```csharp
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

public class MonsterEncounter : MonoBehaviour
{
    [SerializeField] private GameObject _minionPrefab; // 普通小怪
    [SerializeField] private GameObject _bossPrefab;   // 精英大怪
    [SerializeField] private Transform _player;        // 玩家 Transform

    private CwcCreditDirectorModule _director;

    public void StartWave()
    {
        // 1. 创建刷怪导演：
        // 跟随玩家位置，最远 28 米，内环 7 米（防贴脸），总预算 50 点，同屏最多在场 14 点战力
        _director = new CwcCreditDirectorModule(
            center: _player.position,
            radius: 28f,
            totalBudget: 50,
            maxConcurrentCost: 14,
            minWaveCost: 2,
            minRadius: 7f,
            centerTarget: _player
        );

        // 2. 设置波次呼吸时间：每波刷怪之间休息 3.5 秒
        _director.SetWaveCooldown(3.5f);

        // 3. 配置怪群编制：
        // 普通小怪队：每队 3 只，整队消耗 3 点战力，权重 80
        _director.AddItem(new CwcCreditPrefabPlacementItem(_minionPrefab, cost: 3, weight: 80, count: 3));

        // 精英大怪：单只消耗 6 点战力，权重 20，每队 1 只，优先在开阔地刷出
        _director.AddItem(new CwcCreditPrefabPlacementItem(_bossPrefab, cost: 6, weight: 20, count: 1, mode: PlacementMode.OpenCenter));

        // 4. 监听关卡事件
        _director.OnBudgetDepleted += () => Debug.Log("援军用尽，不再产生新敌人！");
        _director.OnClearedAndCompleted += () => Debug.Log("全场敌人肃清，战斗胜利，升起通关宝箱！");

        // 5. 启动导演！
        CwcSceneDirector.Register(_director);
    }

    public void StopWave()
    {
        if (_director != null)
        {
            CwcSceneDirector.Unregister(_director);
            _director = null;
        }
    }
}
```

---

## 常用参数说明

| 参数项 | 说明 | 推荐设置 |
| :--- | :--- | :--- |
| `PlacementMode.Free` | **自由放置**：贴地平整摆放，物体间防重叠互斥 | 适用于大多数杂兵、小石头、野怪 |
| `PlacementMode.WallSnapped` | **靠墙放置**：自动找墙，严丝合缝推开贴好，背墙朝向 | 适用于宝箱、书架、路灯、墙角火把 |
| `PlacementMode.OpenCenter` | **开阔地放置**：自动避开墙角与狭窄过道，选择腹地正中心 | 适用于大体积 Boss、祭坛、核心神龛 |
| `MinLimit / MaxLimit` | **强制保底 / 最大封顶上限**：无论随机数如何，必须在区间内 | 保底神龛至少 1 个，限制金宝箱最多 2 个 |
| `totalBudget` | **总战力预算**：本场遭遇战总额度，扣完即停止刷怪 | 小型战斗 30~50，大型狂潮 100~200 |
| `maxConcurrentCost` | **同屏战力上限**：场上怪物战力达到该值时，导演自动等待 | 控制同屏怪物密度，避免淹没玩家 |
| `minRadius` | **内环安全半径**：怪物出生的最小距离 | 建议 6m~10m，防止怪物突然贴脸骑脸刷新 |
| `SetWaveCooldown` | **波次呼吸冷却**：每波生成后的休息时间 | 建议 2.5s~4.0s，给玩家合理的战斗节奏 |

---

## 演示示例场景 (Demo Scene)

插件自带完整的地牢演示场景，导入后可直接体验全部功能：
- **场景文件**：`Assets/CwcPlugins/CwcSceneDirector/Demo/Demo_SceneDirector.unity`。
- **键盘操作**：
  - 按 **I**：全图均匀摆放宝箱；
  - 按 **C**：一键清除所有宝箱；
  - 按 **E**：开启敌群遭遇战（体验动态预算与波次出怪）；
  - 按 **K**：停止遭遇战；
  - 按 **X**：一键击杀全场敌人（测试清场通关结算）。

---

## 许可协议与商用说明

- 核心代码遵循 [Cwc Tools Public License (Source-Available)](LICENSE) 许可协议：
  - **开发游戏（End Products）**：**个人或商业游戏项目均可免费使用，免版税（Royalty-Free）**，无需支付任何费用。
  - **二次转售限制**：禁止将本插件或修改版本作为独立的资产包、开发工具、插件包发布到 Asset Store、商店或其它公开平台转售。
- 详细说明请查阅 [Third-Party Notices.txt](Third-Party%20Notices.txt)。
