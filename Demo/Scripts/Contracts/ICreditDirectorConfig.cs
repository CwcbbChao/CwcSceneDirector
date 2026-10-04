using System;
using System.Collections.Generic;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 敌群动态遭遇战运行时上下文（纯 C# 类）
    /// 由具体的触发源（如祭坛 Altar、关卡触发盒 TriggerVolume、房间调度器）在激活遭遇战时动态构建
    /// 注入生成中心、总范围半径、战力总预算、同屏并发上限与呼吸冷却等
    /// </summary>
    [Serializable]
    public class EncounterContext
    {
        #region Inspector 序列化字段

        [Header("空间范围 (Spatial Area)")]
        [Tooltip("遭遇战区域的几何中心点（若未指定 CenterTarget 则固定以此为中心）")]
        [SerializeField] private Vector3 _center = Vector3.zero;

        [Tooltip("动态追踪中心目标（若指定则生成中心实时跟随此 Transform，如玩家主角）")]
        [SerializeField] private Transform _centerTarget;

        [Tooltip("最小环形生成半径（米），保证怪物不会贴脸刷新在玩家脚下")]
        [SerializeField] private float _minRadius = 8f;

        [Tooltip("遭遇战总影响区域半径（米）")]
        [SerializeField] private float _radius = 22f;

        [Header("战力预算与并发压力 (Budget & Concurrency)")]
        [Tooltip("本场遭遇战总战力预算（TotalBudget）。由祭坛规模或关卡难度决定，耗尽后停止刷怪")]
        [SerializeField] private int _totalBudget = 50;

        [Tooltip("同屏最大在场威胁度 (MaxConcurrentCost)。场上怪物点数达上限时暂停出怪，杀怪后恢复")]
        [SerializeField] private int _maxConcurrentCost = 14;

        [Tooltip("单波最小可用额度门槛 (MinWaveCost)。可用额度低于此值时不刷新零碎怪")]
        [SerializeField] private int _minWaveCost = 2;

        [Header("战斗节奏 (Pacing)")]
        [Tooltip("波次呼吸冷却时间（秒）。一波怪生成后留给玩家的战斗喘息时间")]
        [SerializeField] private float _waveCooldown = 3.5f;

        [Tooltip("波次决策评估轮询间隔（秒）")]
        [SerializeField] private float _evaluationInterval = 1.0f;

        [Header("局部覆盖 (可选)")]
        [Tooltip("若指定则优先使用该物理设置，否则自动回退到全局 CwcSceneDirectorSettings")]
        [SerializeField] private CwcPlacementSettings _settingsOverride;

        #endregion

        #region 公开属性

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

        public int TotalBudget
        {
            get => _totalBudget;
            set => _totalBudget = Mathf.Max(1, value);
        }

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

        public float WaveCooldown
        {
            get => _waveCooldown;
            set => _waveCooldown = Mathf.Max(0f, value);
        }

        public float EvaluationInterval
        {
            get => _evaluationInterval;
            set => _evaluationInterval = Mathf.Max(0.1f, value);
        }

        public CwcPlacementSettings SettingsOverride
        {
            get => _settingsOverride;
            set => _settingsOverride = value;
        }

        #endregion

        #region 构造函数

        public EncounterContext()
        {
        }

        public EncounterContext(
            Vector3 center,
            float radius = 22f,
            int totalBudget = 50,
            int maxConcurrentCost = 14,
            float waveCooldown = 3.5f,
            CwcPlacementSettings settingsOverride = null,
            float minRadius = 8f,
            Transform centerTarget = null)
        {
            _center = center;
            _centerTarget = centerTarget;
            _minRadius = Mathf.Max(0f, minRadius);
            _radius = Mathf.Max(_minRadius, radius);
            _totalBudget = Mathf.Max(1, totalBudget);
            _maxConcurrentCost = Mathf.Max(1, maxConcurrentCost);
            _waveCooldown = Mathf.Max(0f, waveCooldown);
            _settingsOverride = settingsOverride;
        }

        #endregion
    }

    /// <summary>
    /// 动态信用点导演模块（如狂潮/波次遭遇战）所需的数据配置契约
    /// 配置类只专注定义“要生成什么”（敌群编制与权重），空间总范围、战力预算等由调用上下文传入，物理设置由全局提供
    /// </summary>
    public interface ICreditDirectorConfig
    {
        /// <summary>
        /// 将自身持有的泛型或非泛型条目，统一填充转化为框架可执行的信用点调度项
        /// </summary>
        void PopulateCreditItems(List<CwcCreditPlacementItemBase> outputList);

        /// <summary>
        /// 结合调用方传入的运行时遭遇战上下文，构建可运行的动态信用点导演模块
        /// </summary>
        CwcCreditDirectorModule CreateModule(EncounterContext context);
    }
}
