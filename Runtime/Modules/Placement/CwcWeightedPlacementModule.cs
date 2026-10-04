using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 一次性加权摆放导演模块
    /// 结合暗黑4两级选点（宏观泊松盘均匀采样 + 微观散开校验），采用按比例洗牌配额算法均匀分配各摆放项
    /// 摆放完毕后自动调用 Complete() 退出调度，不占用后续帧资源
    /// </summary>
    public class CwcWeightedPlacementModule : CwcSceneDirectorModule
    {
        #region 常量与静态 (Constants & Static)

        private const string LogPrefix = "[CwcWeightedPlacementModule] ";

        #endregion

        #region 私有字段 (Private Fields)

        private Vector3 _center;
        private float _radius = 30f;
        private float _minClusterDistance = 12f;
        private int _maxClusters = 10;
        private float _frameBudgetMs = 2.0f;
        private bool _useSpatialGrid = true;

        private CwcPlacementSettings _settings;
        private readonly List<CwcPlacementItemBase> _items = new List<CwcPlacementItemBase>(8);

        private readonly List<Vector3> _cachedClusterCenters = new List<Vector3>(32);
        private readonly List<CwcSpatialCell> _cachedCandidateCells = new List<CwcSpatialCell>(128);
        private readonly List<Pose> _cachedPoses = new List<Pose>(16);
        private readonly List<CwcPlacementItemBase> _quotaDeck = new List<CwcPlacementItemBase>(32);

        private Coroutine _placementCoroutine;

        #endregion

        #region 公开属性 (Public Properties)

        public Vector3 Center => _center;
        public float Radius => _radius;
        public float MinClusterDistance => _minClusterDistance;
        public int MaxClusters => _maxClusters;
        public float FrameBudgetMs => _frameBudgetMs;
        public bool UseSpatialGrid
        {
            get => _useSpatialGrid;
            set => _useSpatialGrid = value;
        }
        public IReadOnlyList<CwcPlacementItemBase> Items => _items;
        public CwcPlacementSettings Settings => _settings;

        #endregion

        #region 构造函数 (Constructors)

        public CwcWeightedPlacementModule(
            Vector3 center,
            float radius,
            float minClusterDistance = 12f,
            int maxClusters = 10,
            CwcPlacementSettings settings = null,
            bool useSpatialGrid = true)
        {
            _center = center;
            _radius = Mathf.Max(1f, radius);
            _minClusterDistance = Mathf.Max(1f, minClusterDistance);
            _maxClusters = Mathf.Max(1, maxClusters);
            _settings = settings ?? CwcSceneDirectorSettings.GlobalPlacementSettings;
            _useSpatialGrid = useSpatialGrid;
        }

        #endregion

        #region 模块生命周期 (Lifecycle)

        public override void OnStart()
        {
            base.OnStart();

            if (_items.Count == 0)
            {
                Debug.LogWarning(LogPrefix + "配置项列表为空，无法执行摆放，模块直接结束。");
                Complete();
                return;
            }

            _placementCoroutine = StartCoroutine(RunPlacementRoutine());
        }

        public override void OnStop()
        {
            base.OnStop();

            if (_placementCoroutine != null)
            {
                StopCoroutine(_placementCoroutine);
                _placementCoroutine = null;
            }
        }

        public override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 绘制宏观摆放大圆
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(_center, _radius);

            // 绘制采样的群落中心点
            Gizmos.color = Color.green;
            int count = _cachedClusterCenters.Count;
            for (int i = 0; i < count; i++)
            {
                Gizmos.DrawWireSphere(_cachedClusterCenters[i], 1.0f);
            }
        }

        #endregion

        #region 公开配置链式方法 (Public Methods)

        /// <summary>
        /// 添加加权放置项
        /// </summary>
        public CwcWeightedPlacementModule AddItem(CwcPlacementItemBase item)
        {
            if (item != null && !_items.Contains(item))
            {
                _items.Add(item);
            }
            return this;
        }

        /// <summary>
        /// 便捷添加预制体放置项（极简参数，默认自由放置）
        /// </summary>
        public CwcWeightedPlacementModule AddPrefab(
            GameObject prefab,
            int weight = 10,
            int count = 1,
            PlacementMode mode = PlacementMode.Free)
        {
            return AddItem(new CwcPrefabPlacementItem(prefab, weight, count, mode));
        }

        /// <summary>
        /// 便捷添加纯场景交互物放置项（直接实例化在默认层级下，不走对象池、不占战力、不被超距淘汰）
        /// 交互物默认单点数量为 1，无需配置散开与容器
        /// </summary>
        public CwcWeightedPlacementModule AddInteractable(
            GameObject prefab,
            int weight = 10,
            PlacementMode mode = PlacementMode.Free)
        {
            return AddItem(new CwcInteractablePlacementItem(prefab, weight, mode));
        }

        /// <summary>
        /// 设置单帧允许消耗的最大时间预算（毫秒）
        /// 只要当帧耗时未达预算，将在同一帧内连续快速生成；超出则立即让出主线程，杜绝掉帧
        /// </summary>
        public CwcWeightedPlacementModule SetFrameBudgetMs(float ms)
        {
            _frameBudgetMs = Mathf.Max(0.5f, ms);
            return this;
        }

        #endregion

        #region 私有执行逻辑 (Private Execution)

        private IEnumerator RunPlacementRoutine()
        {
            // 1. 宏观均匀采样：优先尝试使用空间拓扑感知与最远点采样（FPS）
            _cachedClusterCenters.Clear();

            if (_useSpatialGrid && CwcSceneSpatialManager.Instance != null)
            {
                // 优先使用开阔腹地加权最远点采样（Spacious-FPS）：
                // 在全图跨房间离散扩散的同时，优先挑出各大房间的宽阔腹地中心
                float requiredSpread = _settings.DefaultSpreadRadius;
                CwcSceneSpatialManager.Instance.SampleSpaciousPoints(
                    _center,
                    0f,
                    _radius,
                    requiredSpread,
                    _maxClusters,
                    _cachedClusterCenters,
                    _settings);
            }

            // 若未启用空间拓扑感知或未探测到有效格点，平滑安全回退至传统泊松盘算法
            if (_cachedClusterCenters.Count == 0)
            {
                CwcPlacementSpatialUtil.SamplePoissonPoints(
                    _center,
                    _radius,
                    _minClusterDistance,
                    _maxClusters,
                    _cachedClusterCenters);
            }

            int clusterCount = _cachedClusterCenters.Count;
            if (clusterCount == 0)
            {
                Debug.LogWarning(LogPrefix + "空间拓扑感知与泊松盘算法均未能找到任何有效群落中心点，摆放终止。");
                Complete();
                yield break;
            }

            // 2. 按比例构建配额牌堆并洗牌，确保宏观产出比例绝对受控
            BuildAndShuffleQuotaDeck(clusterCount);

            var context = new CwcPlacementContext(
                Director,
                CwcSceneEntityManager.Instance,
                _settings,
                _frameBudgetMs);

            // 3. 遍历各群落中心，按洗牌配额依序发牌生成
            for (int c = 0; c < clusterCount; c++)
            {
                Vector3 clusterCenter = _cachedClusterCenters[c];
                context.ClusterIndex = c;
                context.CurrentClusterCenter = clusterCenter;

                CwcPlacementItemBase selectedItem = _quotaDeck[c];
                if (selectedItem == null) continue;

                int spawnCount = selectedItem.Count;

                // 3.1 微观散开并计算各单位合法位姿
                CollectClusterPoses(clusterCenter, selectedItem, spawnCount, context, _cachedPoses);

                // 3.2 执行配置项的生成协程
                if (_cachedPoses.Count > 0)
                {
                    yield return selectedItem.SpawnRoutine(clusterCenter, _cachedPoses, context);
                }

                // 3.3 检查当前帧预算：若耗时超过预算才让出主线程，时间充裕则直接同帧连出
                if (context.ShouldYield)
                {
                    yield return null;
                    context.ResetFrameTimer();
                }
            }

            // 4. 摆放任务全部完成，自动完成并退出调度
            _placementCoroutine = null;
            Complete();
        }

        private void BuildAndShuffleQuotaDeck(int totalClusters)
        {
            _quotaDeck.Clear();

            int itemCount = _items.Count;
            if (itemCount == 0 || totalClusters <= 0) return;

            int[] itemQuotas = new int[itemCount];
            int allocated = 0;

            // 1. 第一阶段：满足所有项的 MinLimit 强制保底名额
            for (int i = 0; i < itemCount && allocated < totalClusters; i++)
            {
                var item = _items[i];
                if (item == null) continue;

                int minLimit = item.MinLimit;
                if (minLimit > 0)
                {
                    int grant = Mathf.Min(minLimit, totalClusters - allocated);
                    if (item.MaxLimit > 0)
                    {
                        grant = Mathf.Min(grant, item.MaxLimit);
                    }
                    itemQuotas[i] += grant;
                    allocated += grant;
                }
            }

            // 2. 第二阶段：剩余空余名额按 Weight 权重比例分配给未达 MaxLimit 的项
            int remaining = totalClusters - allocated;
            if (remaining > 0)
            {
                int eligibleWeight = 0;
                for (int i = 0; i < itemCount; i++)
                {
                    var item = _items[i];
                    if (item != null && (item.MaxLimit == 0 || itemQuotas[i] < item.MaxLimit))
                    {
                        eligibleWeight += item.Weight;
                    }
                }

                if (eligibleWeight > 0)
                {
                    for (int i = 0; i < itemCount && allocated < totalClusters; i++)
                    {
                        var item = _items[i];
                        if (item == null) continue;

                        if (item.MaxLimit > 0 && itemQuotas[i] >= item.MaxLimit) continue;

                        int quota = Mathf.RoundToInt((float)item.Weight / eligibleWeight * remaining);
                        if (quota <= 0 && item.Weight > 0 && itemQuotas[i] == 0)
                        {
                            quota = 1; // 只要配置了权重且尚未分配，保底分配 1 个
                        }

                        if (item.MaxLimit > 0)
                        {
                            quota = Mathf.Min(quota, item.MaxLimit - itemQuotas[i]);
                        }

                        quota = Mathf.Min(quota, totalClusters - allocated);
                        if (quota > 0)
                        {
                            itemQuotas[i] += quota;
                            allocated += quota;
                        }
                    }
                }
            }

            // 3. 第三阶段：若仍有剩余名额（由于舍入或无未达上限项），优先补给未达上限的最高权重项
            while (allocated < totalClusters)
            {
                int bestIdx = -1;
                int maxWeight = -1;

                for (int i = 0; i < itemCount; i++)
                {
                    var item = _items[i];
                    if (item == null) continue;
                    if (item.MaxLimit > 0 && itemQuotas[i] >= item.MaxLimit) continue;

                    if (item.Weight > maxWeight)
                    {
                        maxWeight = item.Weight;
                        bestIdx = i;
                    }
                }

                if (bestIdx == -1)
                {
                    // 若所有项均达到最大封顶上限，使用首项填满防止停滞
                    bestIdx = 0;
                }

                itemQuotas[bestIdx]++;
                allocated++;
            }

            // 4. 将配额展开装入牌堆
            for (int i = 0; i < itemCount; i++)
            {
                var item = _items[i];
                int qCount = itemQuotas[i];
                for (int q = 0; q < qCount; q++)
                {
                    _quotaDeck.Add(item);
                }
            }

            // 5. Fisher-Yates 随机洗牌打乱，使分布具有不可预测的探索感
            for (int i = _quotaDeck.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var temp = _quotaDeck[i];
                _quotaDeck[i] = _quotaDeck[j];
                _quotaDeck[j] = temp;
            }
        }

        private void CollectClusterPoses(
            Vector3 clusterCenter,
            CwcPlacementItemBase item,
            int targetCount,
            CwcPlacementContext context,
            List<Pose> results)
        {
            if (item != null)
            {
                item.CollectPlacementPoses(clusterCenter, targetCount, context, results);
            }
            else
            {
                results.Clear();
            }
        }

        #endregion
    }
}
