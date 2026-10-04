using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 动态信用点敌群刷怪导演模块
    /// 结合暗黑4世界狂潮与雨中冒险2 AI Director 机制，基于激活总额度（TotalBudget）与同屏在场上限（MaxConcurrentCost）进行动态评估；
    /// 采用“目标锁定防饥饿机制（Target Lock）”与“波次呼吸冷却（Wave Cooldown）”，实现张弛有度的高品质战斗节奏
    /// </summary>
    public class CwcCreditDirectorModule : CwcSceneDirectorModule
    {
        #region 常量与静态 (Constants & Static)

        private const string LogPrefix = "[CwcCreditDirectorModule] ";

        #endregion

        #region 私有字段 (Private Fields)

        private Vector3 _center;
        private Transform _centerTarget;
        private float _minRadius = 8f;
        private float _radius = 35f;

        private int _totalBudget = 100;
        private int _remainingBudget = 100;
        private int _maxConcurrentCost = 25;
        private int _minWaveCost = 2;

        private float _evaluationInterval = 1.0f;
        private float _waveCooldown = 3.5f;
        private float _cooldownTimer = 0f;

        private bool _autoCompleteWhenCleared = true;
        private bool _hasTriggeredDepleted = false;

        private CwcPlacementSettings _settings;
        private readonly List<CwcCreditPlacementItemBase> _items = new List<CwcCreditPlacementItemBase>(8);
        private readonly List<Pose> _cachedPoses = new List<Pose>(16);

        private CwcCreditPlacementItemBase _pendingTarget = null;
        private Coroutine _directorCoroutine;

        #endregion

        #region 公开事件与属性 (Events & Properties)

        /// <summary>
        /// 当总预算全部消耗殆尽时触发（可用于播放狂潮即将结束警告或UI提示）
        /// </summary>
        public event Action OnBudgetDepleted;

        /// <summary>
        /// 当总预算耗尽且场上所有怪物被完全清场时触发（用于升起战利品大宝箱或打开门禁）
        /// </summary>
        public event Action OnClearedAndCompleted;

        public Vector3 Center
        {
            get => _center;
            set => _center = value;
        }

        public Transform CenterTarget
        {
            get => _centerTarget;
            set => _centerTarget = value;
        }

        /// <summary>
        /// 当前生效的中心坐标（若绑定了目标 Transform 则跟随其实时世界坐标，否则使用静态 Center）
        /// </summary>
        public Vector3 CurrentCenter => _centerTarget != null ? _centerTarget.position : _center;

        public float MinRadius
        {
            get => _minRadius;
            set => _minRadius = Mathf.Max(0f, value);
        }

        public float Radius
        {
            get => _radius;
            set => _radius = Mathf.Max(_minRadius, value);
        }

        public int TotalBudget => _totalBudget;

        public int RemainingBudget => _remainingBudget;

        public int MaxConcurrentCost
        {
            get => _maxConcurrentCost;
            set => _maxConcurrentCost = Mathf.Max(1, value);
        }

        public int MinWaveCost
        {
            get => _minWaveCost;
            set => _minWaveCost = Mathf.Max(1, value);
        }

        public float EvaluationInterval
        {
            get => _evaluationInterval;
            set => _evaluationInterval = Mathf.Max(0.2f, value);
        }

        public float WaveCooldown
        {
            get => _waveCooldown;
            set => _waveCooldown = Mathf.Max(0f, value);
        }

        public bool AutoCompleteWhenCleared
        {
            get => _autoCompleteWhenCleared;
            set => _autoCompleteWhenCleared = value;
        }

        public CwcCreditPlacementItemBase PendingTarget => _pendingTarget;

        public IReadOnlyList<CwcCreditPlacementItemBase> Items => _items;

        #endregion

        #region 构造函数 (Constructors)

        public CwcCreditDirectorModule(
            Vector3 center,
            float radius,
            int totalBudget,
            int maxConcurrentCost = 25,
            int minWaveCost = 2,
            CwcPlacementSettings settings = null,
            float minRadius = 8f,
            Transform centerTarget = null)
        {
            _center = center;
            _centerTarget = centerTarget;
            _minRadius = Mathf.Max(0f, minRadius);
            _radius = Mathf.Max(_minRadius, radius);
            _totalBudget = Mathf.Max(1, totalBudget);
            _remainingBudget = _totalBudget;
            _maxConcurrentCost = Mathf.Max(1, maxConcurrentCost);
            _minWaveCost = Mathf.Max(1, minWaveCost);
            _settings = settings ?? CwcSceneDirectorSettings.GlobalPlacementSettings;
        }

        #endregion

        #region 模块生命周期 (Lifecycle)

        public override void OnStart()
        {
            base.OnStart();

            if (_items.Count == 0)
            {
                Debug.LogWarning(LogPrefix + "配置项列表为空，无法运行动态刷怪导演，模块直接结束。");
                Complete();
                return;
            }

            _directorCoroutine = StartCoroutine(RunDirectorRoutine());
        }

        public override void OnStop()
        {
            base.OnStop();

            if (_directorCoroutine != null)
            {
                StopCoroutine(_directorCoroutine);
                _directorCoroutine = null;
            }
        }

        public override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Vector3 currentCenter = CurrentCenter;

            // 绘制内环安全隔离圈（黄橙色线框，防止骑脸刷怪）
            if (_minRadius > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.35f);
                Gizmos.DrawWireSphere(currentCenter, _minRadius);
            }

            // 绘制外环遭遇战总影响范围（红色线框）
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(currentCenter, _radius);
        }

        #endregion

        #region 公开控制与动态配置方法 (Public Methods)

        /// <summary>
        /// 动态更新生成中心坐标（方便外部无目标 Transform 时手动推送最新坐标）
        /// </summary>
        public void UpdateCenter(Vector3 newCenter)
        {
            _center = newCenter;
        }

        /// <summary>
        /// 添加带消耗的信用点配置项
        /// </summary>
        public CwcCreditDirectorModule AddItem(CwcCreditPlacementItemBase item)
        {
            if (item != null && !_items.Contains(item))
            {
                _items.Add(item);
            }
            return this;
        }

        /// <summary>
        /// 便捷添加预制体信用点放置项
        /// </summary>
        public CwcCreditDirectorModule AddPrefab(
            GameObject prefab,
            int cost,
            int weight = 10,
            int count = 1,
            PlacementMode mode = PlacementMode.Free)
        {
            return AddItem(new CwcCreditPrefabPlacementItem(prefab, cost, weight, count, mode));
        }

        /// <summary>
        /// 动态向事件追加可用预算（例如难度提升、波次加剧时）
        /// </summary>
        public void AddBudget(int amount)
        {
            if (amount <= 0) return;

            _totalBudget += amount;
            _remainingBudget += amount;
            _hasTriggeredDepleted = false;
        }

        /// <summary>
        /// 设置波次呼吸冷却时间
        /// </summary>
        public CwcCreditDirectorModule SetWaveCooldown(float cooldown)
        {
            _waveCooldown = Mathf.Max(0f, cooldown);
            return this;
        }

        /// <summary>
        /// 实体出池激活时由单位管理器自动调用的实收扣费接口（单向自动审计）
        /// 支持负蓝透支，实生多少扣多少，绝不吞没任何预算差额
        /// </summary>
        public virtual void ConsumeBudget(int amount)
        {
            if (amount <= 0) return;

            _remainingBudget -= amount;

            if (_remainingBudget <= 0 && !_hasTriggeredDepleted)
            {
                _hasTriggeredDepleted = true;
                OnBudgetDepleted?.Invoke();
            }
        }

        #endregion

        #region 私有执行逻辑 (Private Execution)

        private IEnumerator RunDirectorRoutine()
        {
            var context = new CwcPlacementContext(
                Director,
                CwcSceneEntityManager.Instance,
                _settings,
                ownerModule: this);

            while (!IsCompleted)
            {
                float step = _evaluationInterval;
                yield return new WaitForSeconds(step);

                if (IsPaused || !IsEnabled) continue;

                // 1. 推进波次呼吸冷却期
                if (_cooldownTimer > 0f)
                {
                    _cooldownTimer -= step;
                    continue;
                }

                // 2. 借助单位管理器，感知区域当前在场总威胁度（ThreatCost，跟随当前生效中心）
                Vector3 currentCenter = CurrentCenter;
                int currentInAreaCost = context.EntityManager.GetTotalCostInRadius(currentCenter, _radius);

                // 3. 检查总预算耗尽与清场结算
                if (_remainingBudget <= 0)
                {
                    if (!_hasTriggeredDepleted)
                    {
                        _hasTriggeredDepleted = true;
                        OnBudgetDepleted?.Invoke();
                    }

                    // 若预算耗尽且场上所有怪物已经被玩家肃清，触发结算并自销毁
                    if (_autoCompleteWhenCleared && currentInAreaCost == 0)
                    {
                        OnClearedAndCompleted?.Invoke();
                        _directorCoroutine = null;
                        Complete();
                        yield break;
                    }

                    // 仍在等待玩家清空场上残余怪物
                    continue;
                }

                // 4. 检查在场同屏容量限制
                int availableCapacity = _maxConcurrentCost - currentInAreaCost;
                if (availableCapacity < _minWaveCost && currentInAreaCost > 0)
                {
                    continue; // 场上当前怪群过于密集，等待玩家杀怪腾出战力空间
                }

                // 5. 目标锁定与决策（Target Lock 防饥饿机制）
                if (_pendingTarget == null)
                {
                    _pendingTarget = SelectWeightedItem();
                }

                if (_pendingTarget == null) continue;

                // 6. 软上限决策放行（负蓝透支机制）：
                // 只要剩余总预算大于 0，即便当前目标 Cost 超出剩余预算，也允许整队完整买下并透支到负数！
                // 仅当场上已有很多怪物且该目标会导致严重超载时才暂缓等待
                if (_pendingTarget.Cost > availableCapacity && currentInAreaCost >= Mathf.Max(1, _maxConcurrentCost * 0.7f))
                {
                    continue;
                }

                // 7. 额度充裕或允许透支，放行生成目标怪群！
                // 注意：不再进行生硬的提前预扣款，由各实体在真正激活出池时，由 EntityManager 自动按件实扣
                float spreadRadius = _pendingTarget.SpreadRadius;

                // 宏观带半径选点：在圆环范围内寻找能容纳该小队展开腹地的开阔群落中心
                Vector3 clusterCenter = SampleClusterCenter(context, spreadRadius);

                // 微观散开并计算各单位位姿（带满额保底与弹性向心回弹）
                CollectClusterPoses(clusterCenter, _pendingTarget, _pendingTarget.Count, context, _cachedPoses);

                // 协程分帧执行生成
                if (_cachedPoses.Count > 0)
                {
                    yield return _pendingTarget.SpawnRoutine(clusterCenter, _cachedPoses, context);
                }

                // 8. 成功生成，清除锁定目标，并进入波次呼吸冷却期
                _pendingTarget = null;
                _cooldownTimer = _waveCooldown;
            }

            _directorCoroutine = null;
        }

        private CwcCreditPlacementItemBase SelectWeightedItem()
        {
            int totalWeight = 0;
            int count = _items.Count;
            for (int i = 0; i < count; i++)
            {
                totalWeight += _items[i].Weight;
            }

            if (totalWeight <= 0) return null;

            int roll = UnityEngine.Random.Range(0, totalWeight);
            int accumulated = 0;

            for (int i = 0; i < count; i++)
            {
                accumulated += _items[i].Weight;
                if (roll < accumulated)
                {
                    return _items[i];
                }
            }

            return _items[0];
        }

        private Vector3 SampleClusterCenter(CwcPlacementContext context, float spreadRadius = 4f)
        {
            Vector3 center = CurrentCenter;

            // 1. 优先尝试使用空间管理器的点云拓扑算法：在环形范围内挑选腹地最大、点云最饱满的理想中心（纯数学拓扑，零射线开销）
            if (CwcSceneSpatialManager.HasInstance || CwcSceneSpatialManager.Instance != null)
            {
                if (CwcSceneSpatialManager.Instance.TrySampleSpaciousCenter(center, _minRadius, _radius, spreadRadius, out Vector3 spaciousCenter, _settings))
                {
                    return spaciousCenter;
                }
            }

            // 2. 若场景未配置空间管理器，平滑安全回退至纯物理射线探针抽样
            int maxAttempts = _settings.MaxValidationAttempts * 2;
            Vector3 fallbackPoint = center;
            bool foundFallback = false;

            for (int i = 0; i < maxAttempts; i++)
            {
                Vector3 candidate = CwcPlacementSpatialUtil.SampleAnnulusPoint(center, _minRadius, _radius);

                if (CwcPlacementSpatialUtil.TrySnapToGround(candidate, _settings, out Vector3 groundPoint))
                {
                    if (CwcPlacementSpatialUtil.IsObstacleFree(groundPoint, _settings))
                    {
                        // 宏观带半径选点：检查以小队展开半径的一半为范围的开阔度，优先寻找能兜住小队的宽阔腹地
                        float checkRadius = Mathf.Min(spreadRadius * 0.5f, 2.5f);
                        LayerMask obstacleMask = _settings.EffectiveObstacleLayer;
                        bool isWideArea = obstacleMask == 0 || !Physics.CheckSphere(groundPoint + Vector3.up * 0.5f, checkRadius, obstacleMask);

                        if (isWideArea)
                        {
                            return groundPoint; // 找到了宽阔平坦的理想群落中心
                        }

                        if (!foundFallback)
                        {
                            fallbackPoint = groundPoint;
                            foundFallback = true;
                        }
                    }
                }
            }

            return foundFallback ? fallbackPoint : center;
        }

        private void CollectClusterPoses(
            Vector3 clusterCenter,
            CwcCreditPlacementItemBase item,
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
