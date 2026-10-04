using System;
using System.Collections.Generic;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 加权放置运行时空间与规格上下文（纯 C# 类）
    /// 由触发源（如关卡区域、房间原点、箱子群分布点）在激活生成时动态构建并传入
    /// 包含生成中心、总范围半径、群落数量要求与排斥距离等
    /// </summary>
    [Serializable]
    public class PlacementAreaContext
    {
        #region Inspector 序列化字段

        [Header("空间范围 (Spatial Area)")]
        [Tooltip("摆放区域的几何中心点")]
        [SerializeField] private Vector3 _center = Vector3.zero;

        [Tooltip("摆放影响区域半径（米）")]
        [SerializeField] private float _radius = 25f;

        [Header("生成规格 (Placement Specs)")]
        [Tooltip("最多生成的群落总数")]
        [Min(1)]
        [SerializeField] private int _maxClusters = 8;

        [Tooltip("群落间最小排斥间距（米，泊松盘采样最小间距）")]
        [SerializeField] private float _minClusterDistance = 6f;

        [Tooltip("是否优先使用场景空间拓扑感知与最远点采样（FPS），为 false 则使用传统泊松盘")]
        [SerializeField] private bool _useSpatialGrid = true;

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

        public float Radius
        {
            get => _radius;
            set => _radius = Mathf.Max(1f, value);
        }

        public int MaxClusters
        {
            get => _maxClusters;
            set => _maxClusters = Mathf.Max(1, value);
        }

        public float MinClusterDistance
        {
            get => _minClusterDistance;
            set => _minClusterDistance = Mathf.Max(1f, value);
        }

        public bool UseSpatialGrid
        {
            get => _useSpatialGrid;
            set => _useSpatialGrid = value;
        }

        public CwcPlacementSettings SettingsOverride
        {
            get => _settingsOverride;
            set => _settingsOverride = value;
        }

        #endregion

        #region 构造函数

        public PlacementAreaContext()
        {
        }

        public PlacementAreaContext(
            Vector3 center,
            float radius = 25f,
            int maxClusters = 8,
            float minClusterDistance = 6f,
            CwcPlacementSettings settingsOverride = null,
            bool useSpatialGrid = true)
        {
            _center = center;
            _radius = Mathf.Max(1f, radius);
            _maxClusters = Mathf.Max(1, maxClusters);
            _minClusterDistance = Mathf.Max(1f, minClusterDistance);
            _settingsOverride = settingsOverride;
            _useSpatialGrid = useSpatialGrid;
        }

        #endregion
    }

    /// <summary>
    /// 加权单次放置模块（如交互物/静态野怪）所需的数据配置契约
    /// 配置类只专注定义“要生成什么”，空间总范围与规格由调用上下文传入，物理设置由全局提供
    /// </summary>
    public interface IWeightedPlacementConfig
    {
        /// <summary>
        /// 将自身持有的泛型或非泛型条目，统一填充转化为框架可执行的调度项
        /// </summary>
        void PopulatePlacementItems(List<CwcPlacementItemBase> outputList);

        /// <summary>
        /// 结合调用方传入的运行时空间上下文，构建可运行的加权放置模块
        /// </summary>
        CwcWeightedPlacementModule CreateModule(PlacementAreaContext context);
    }
}
