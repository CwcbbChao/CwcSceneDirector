using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 加权放置项多态调度基类
    /// 仅包含比重、数量、放置模式与散开半径等极简通用字段，具有直观开箱即用的默认值
    /// </summary>
    [Serializable]
    public abstract class CwcPlacementItemBase
    {
        #region 静态缓冲 (Static Buffers)

        private static readonly List<Vector3> s_SunflowerBuffer = new List<Vector3>(32);

        #endregion

        #region Inspector 序列化字段 (Serialized Fields)

        [Tooltip("Quota weight ratio")]
        [SerializeField] private int _weight = 10;

        [Tooltip("Spawn count per cluster")]
        [SerializeField] private int _count = 1;

        [Tooltip("Placement mode")]
        [SerializeField] private PlacementMode _mode = PlacementMode.Free;

        [Tooltip("Footprint radius in meters (0 for auto)")]
        [SerializeField] private float _footprintRadius = 0f;

        [Tooltip("Minimum guaranteed count (0 for none)")]
        [SerializeField] private int _minLimit = 0;

        [Tooltip("Maximum allowed count (0 for none)")]
        [SerializeField] private int _maxLimit = 0;

        [Tooltip("Spread radius in meters (0 for default)")]
        [SerializeField] private float _spreadRadius = 0f;

        #endregion

        #region 私有非序列化字段 (Private Fields)

        private IPlacementStrategy _strategy;

        #endregion

        #region 公开属性 (Public Properties)

        public int Weight
        {
            get => _weight;
            set => _weight = Mathf.Max(1, value);
        }

        public int Count
        {
            get => _count;
            set => _count = Mathf.Max(1, value);
        }

        public int MinLimit
        {
            get => _minLimit;
            set => _minLimit = Mathf.Max(0, value);
        }

        public int MaxLimit
        {
            get => _maxLimit;
            set => _maxLimit = Mathf.Max(0, value);
        }

        /// <summary>
        /// 实体战力威胁度开销（单一真实数据源，由子类或载体自描述，默认为 1）
        /// </summary>
        public virtual int ThreatCost
        {
            get => 1;
            set { }
        }

        public PlacementMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                _strategy = CwcPlacementStrategies.FromMode(value);
            }
        }

        public IPlacementStrategy Strategy
        {
            get => _strategy ?? CwcPlacementStrategies.FromMode(_mode);
            set => _strategy = value;
        }

        public float FootprintRadius
        {
            get => _footprintRadius;
            set => _footprintRadius = Mathf.Max(0f, value);
        }

        public float SpreadRadius
        {
            get => _spreadRadius;
            set => _spreadRadius = Mathf.Max(0f, value);
        }

        #endregion

        #region 虚方法：位姿计算与生成契约 (Virtual Methods)

        /// <summary>
        /// 计算候选落点的最终摆放位姿（坐标与朝向）
        /// 直接委托给多态策略接口 IPlacementStrategy 执行，彻底解耦硬编码分支并由子类及策略驱动
        /// </summary>
        public virtual bool TryCalculatePlacementPose(
            Vector3 candidatePoint,
            Vector3 clusterCenter,
            CwcPlacementContext context,
            out Pose finalPose)
        {
            var strategy = Strategy ?? CwcPlacementStrategies.Free;
            return strategy.TryProcessPose(candidatePoint, clusterCenter, FootprintRadius, context, out finalPose);
        }

        /// <summary>
        /// 采集本批次生成的微观位姿列表
        /// 默认采用葵花黄金分割点阵（Sunflower Spiral）算法，保证单位极度均匀、无扎堆地分布在半径内
        /// 支持子类自由重写以实现其他自定义战术阵型
        /// </summary>
        public virtual void CollectPlacementPoses(
            Vector3 clusterCenter,
            int targetCount,
            CwcPlacementContext context,
            List<Pose> results)
        {
            results.Clear();
            if (targetCount <= 0) return;

            float spreadRadius = _spreadRadius > 0f ? _spreadRadius : context.Settings.DefaultSpreadRadius;
            float innerRadius = Mathf.Max(0.6f, _footprintRadius > 0f ? _footprintRadius * 1.5f : context.Settings.EntitySeparationRadius);

            // 1. 生成葵花黄金角均匀候选点阵
            CwcPlacementSpatialUtil.SampleSunflowerPoints(
                clusterCenter,
                spreadRadius,
                targetCount,
                s_SunflowerBuffer,
                innerRadius);

            int candidateCount = s_SunflowerBuffer.Count;

            // 2. 依序进行物理与空间有效性判定
            for (int i = 0; i < candidateCount; i++)
            {
                Vector3 candidate = s_SunflowerBuffer[i];

                if (TryCalculatePlacementPose(candidate, clusterCenter, context, out Pose validPose))
                {
                    results.Add(validPose);
                }
                else
                {
                    bool resolved = false;

                    // 2.1 阵型弹性向心收缩（Inward Bounce）：若外圈撞墙/悬崖，沿半径向群落中心方向回缩试探可用腹地
                    Vector3 toCenter = clusterCenter - candidate;
                    if (toCenter.sqrMagnitude > 0.36f)
                    {
                        if (TryCalculatePlacementPose(candidate + toCenter * 0.4f, clusterCenter, context, out Pose inwardPose1))
                        {
                            results.Add(inwardPose1);
                            resolved = true;
                        }
                        else if (TryCalculatePlacementPose(candidate + toCenter * 0.7f, clusterCenter, context, out Pose inwardPose2))
                        {
                            results.Add(inwardPose2);
                            resolved = true;
                        }
                    }

                    // 2.2 若向心收缩未通过，尝试微量方向扰动（最多3次）
                    if (!resolved)
                    {
                        for (int attempt = 0; attempt < 3; attempt++)
                        {
                            float jitterAngle = UnityEngine.Random.value * Mathf.PI * 2f;
                            float jitterDist = UnityEngine.Random.Range(0.4f, 1.0f);
                            Vector3 jittered = candidate + new Vector3(Mathf.Cos(jitterAngle), 0f, Mathf.Sin(jitterAngle)) * jitterDist;

                            if (TryCalculatePlacementPose(jittered, clusterCenter, context, out Pose altPose))
                            {
                                results.Add(altPose);
                                resolved = true;
                                break;
                            }
                        }
                    }
                }
            }

            // 3. 满额保底补齐（No Monster Left Behind）：
            // 若因局部极端地形阻挡仍有缺失名额，绝不直接吞怪，在有效散开半径内随机采样平地补足名额
            int missingCount = targetCount - results.Count;
            if (missingCount > 0)
            {
                int maxFallbackAttempts = missingCount * 6;
                for (int a = 0; a < maxFallbackAttempts && results.Count < targetCount; a++)
                {
                    float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                    float dist = Mathf.Sqrt(UnityEngine.Random.value) * spreadRadius;
                    Vector3 fallbackCandidate = clusterCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;

                    if (TryCalculatePlacementPose(fallbackCandidate, clusterCenter, context, out Pose fallbackPose))
                    {
                        results.Add(fallbackPose);
                    }
                }
            }
        }

        /// <summary>
        /// 核心生成执行协程
        /// 接收已完成空间校验的位姿列表，由子类实现具体的出池、参数注入、修饰与激活逻辑
        /// </summary>
        public abstract IEnumerator SpawnRoutine(
            Vector3 clusterCenter,
            IReadOnlyList<Pose> poses,
            CwcPlacementContext context);

        #endregion
    }

    /// <summary>
    /// 泛型加权放置项基类
    /// 将配置数据源（如特定 SO、预制体或数据类）作为泛型强类型绑定，实现高内聚装配
    /// </summary>
    [Serializable]
    public abstract class CwcPlacementItem<TData> : CwcPlacementItemBase
    {
        [Tooltip("Target configuration data")]
        [SerializeField] private TData _data;

        public TData Data
        {
            get => _data;
            set => _data = value;
        }

        protected CwcPlacementItem()
        {
        }

        protected CwcPlacementItem(TData data, int weight = 10, int count = 1, PlacementMode mode = PlacementMode.Free)
        {
            _data = data;
            Weight = weight;
            Count = count;
            Mode = mode;
        }
    }

    /// <summary>
    /// 开箱即用的预制体直接放置项
    /// 适用于宝箱、神龛、单体怪等直接由 Prefab 定义的放置物
    /// </summary>
    [Serializable]
    public class CwcPrefabPlacementItem : CwcPlacementItem<GameObject>
    {
        public CwcPrefabPlacementItem()
        {
        }

        public CwcPrefabPlacementItem(
            GameObject prefab,
            int weight = 10,
            int count = 1,
            IPlacementStrategy strategy = null)
            : base(prefab, weight, count, PlacementMode.Free)
        {
            Strategy = strategy ?? CwcPlacementStrategies.Free;
            FootprintRadius = CwcPlacementSpatialUtil.GetPhysicalFootprintRadius(prefab);
        }

        public CwcPrefabPlacementItem(
            GameObject prefab,
            int weight,
            int count,
            PlacementMode mode)
            : base(prefab, weight, count, mode)
        {
            FootprintRadius = CwcPlacementSpatialUtil.GetPhysicalFootprintRadius(prefab);
        }

        public override bool TryCalculatePlacementPose(
            Vector3 candidatePoint,
            Vector3 clusterCenter,
            CwcPlacementContext context,
            out Pose finalPose)
        {
            if (FootprintRadius <= 0f && Data != null)
            {
                FootprintRadius = CwcPlacementSpatialUtil.GetPhysicalFootprintRadius(Data);
            }
            return base.TryCalculatePlacementPose(candidatePoint, clusterCenter, context, out finalPose);
        }

        public override IEnumerator SpawnRoutine(
            Vector3 clusterCenter,
            IReadOnlyList<Pose> poses,
            CwcPlacementContext context)
        {
            if (Data == null) yield break;

            int count = poses.Count;
            for (int i = 0; i < count; i++)
            {
                Pose pose = poses[i];
                var hook = context.EntityManager.Get(Data);
                if (hook != null)
                {
                    hook.transform.position = pose.position;
                    hook.transform.rotation = pose.rotation;
                    hook.gameObject.SetActive(true);
                }

                // 仅当当前帧耗时超出性能预算时才让出主线程，帧时间充裕则同帧极速创建
                if (context.ShouldYield)
                {
                    yield return null;
                    context.ResetFrameTimer();
                }
            }
        }
    }

    /// <summary>
    /// 开箱即用的纯场景交互物放置项
    /// 直接实例化到默认层级下，不受 EntityManager 对象池管理、不占战力预算、不触发超距淘汰
    /// 适用于宝箱、神龛、资源矿石、传送门等静态场景物件，天然单体放置（数量恒为 1）
    /// </summary>
    [Serializable]
    public class CwcInteractablePlacementItem : CwcPlacementItem<GameObject>
    {
        private const string DefaultContainerName = "[Interactables]";

        public override int ThreatCost => 0; // 交互物战力消耗严格为 0

        public CwcInteractablePlacementItem()
        {
            Count = 1;
            SpreadRadius = 0f;
        }

        public CwcInteractablePlacementItem(
            GameObject prefab,
            int weight = 10,
            PlacementMode mode = PlacementMode.Free,
            IPlacementStrategy strategy = null)
            : base(prefab, weight, 1, mode)
        {
            Count = 1;
            SpreadRadius = 0f;
            Strategy = strategy ?? CwcPlacementStrategies.FromMode(mode);
            FootprintRadius = CwcPlacementSpatialUtil.GetPhysicalFootprintRadius(prefab);
        }

        public override bool TryCalculatePlacementPose(
            Vector3 candidatePoint,
            Vector3 clusterCenter,
            CwcPlacementContext context,
            out Pose finalPose)
        {
            if (FootprintRadius <= 0f && Data != null)
            {
                FootprintRadius = CwcPlacementSpatialUtil.GetPhysicalFootprintRadius(Data);
            }
            return base.TryCalculatePlacementPose(candidatePoint, clusterCenter, context, out finalPose);
        }

        public override IEnumerator SpawnRoutine(
            Vector3 clusterCenter,
            IReadOnlyList<Pose> poses,
            CwcPlacementContext context)
        {
            if (Data == null) yield break;

            int count = poses.Count;
            Transform parentContainer = GetOrCreateContainer();

            for (int i = 0; i < count; i++)
            {
                Pose pose = poses[i];
                GameObject instance = UnityEngine.Object.Instantiate(Data, pose.position, pose.rotation, parentContainer);

                // 若预制体上偶然残留挂载了 CwcSceneEntityHook，立即安全移除，杜绝其自动向实体管理器报到引发战力与淘汰污染
                if (instance.TryGetComponent<CwcSceneEntityHook>(out var hook))
                {
                    UnityEngine.Object.Destroy(hook);
                }

                // 仅当当前帧耗时超出性能预算时才让出主线程
                if (context.ShouldYield)
                {
                    yield return null;
                    context.ResetFrameTimer();
                }
            }
        }

        private static Transform GetOrCreateContainer()
        {
            GameObject containerGo = GameObject.Find(DefaultContainerName);
            if (containerGo == null)
            {
                containerGo = new GameObject(DefaultContainerName);
            }
            return containerGo.transform;
        }
    }
}
