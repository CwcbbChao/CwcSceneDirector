using System;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 宏观选点与物理空间校验配置
    /// 集中管理地面吸附、障碍墙体探测、靠墙与开阔地算法参数
    /// </summary>
    [Serializable]
    public class CwcPlacementSettings
    {
        [Tooltip("Ground collision layer mask")]
        public LayerMask GroundLayer = 1 << 0;

        [Tooltip("Raycast start height above target")]
        public float RaycastOriginHeight = 10f;

        [Tooltip("Maximum raycast depth")]
        public float MaxRaycastDistance = 25f;

        [Tooltip("Enable NavMesh validation and snap")]
        public bool UseNavMesh = true;

        [Tooltip("NavMesh sample tolerance radius")]
        public float NavMeshSampleRange = 2f;

        [Tooltip("Wall and boundary collision layer mask")]
        public LayerMask WallLayer = 0;

        [Tooltip("Obstacle and interactable collision layer mask")]
        public LayerMask ObstacleLayer = 0;

        [Tooltip("Obstacle check sphere radius")]
        public float ObstacleCheckRadius = 0.5f;

        /// <summary>
        /// 获取有效的墙体碰撞层级（若未单独配置 WallLayer，自动平滑回退兼容 ObstacleLayer）
        /// </summary>
        public LayerMask EffectiveWallLayer => WallLayer.value != 0 ? WallLayer : ObstacleLayer;

        /// <summary>
        /// 获取有效的障碍阻挡碰撞层级（综合合并独立障碍物、交互物与墙壁，防止穿模）
        /// </summary>
        public LayerMask EffectiveObstacleLayer => ObstacleLayer.value | WallLayer.value;

        [Tooltip("Max horizontal distance to search walls for back-to-wall policy")]
        public float WallSearchDistance = 3.5f;

        [Tooltip("Safety pushback offset along wall normal in meters")]
        public float WallOffset = 0.6f;

        [Tooltip("Min distance to nearest wall for clearing policy")]
        public float OpenSpaceMinWallDistance = 3.0f;

        [Tooltip("Default spread radius in meters")]
        public float DefaultSpreadRadius = 4.0f;

        [Tooltip("Entity separation radius in meters (0 for disabled)")]
        public float EntitySeparationRadius = 1.0f;

        [Tooltip("Max validation retry attempts per sample")]
        public int MaxValidationAttempts = 10;
    }

    /// <summary>
    /// 场景导演全局静态配置与默认物理环境策略
    /// 支持直接静态查询，支持在游戏启动或运行时随时覆盖
    /// 彻底将项目物理规则与业务生成配置解耦
    /// </summary>
    public static class CwcSceneDirectorSettings
    {
        #region 私有静态字段 (Private Static Fields)

        private static CwcPlacementSettings s_GlobalPlacementSettings = new CwcPlacementSettings
        {
            GroundLayer = 1 << 0,
            ObstacleLayer = 0,
            RaycastOriginHeight = 12f,
            MaxRaycastDistance = 25f,
            UseNavMesh = false, // 默认纯物理射线吸附，对未烘焙 NavMesh 场景开箱即用
            NavMeshSampleRange = 2f,
            ObstacleCheckRadius = 0.35f,
            WallSearchDistance = 6f,
            WallOffset = 0.6f,
            OpenSpaceMinWallDistance = 3.0f,
            DefaultSpreadRadius = 4.0f,
            EntitySeparationRadius = 1.0f,
            MaxValidationAttempts = 10
        };

        private static float s_GlobalFrameBudgetMs = 2.0f;

        #endregion

        #region 公开属性 (Public Properties)

        /// <summary>
        /// 全局默认物理与空间探测策略
        /// </summary>
        public static CwcPlacementSettings GlobalPlacementSettings
        {
            get => s_GlobalPlacementSettings;
            set => s_GlobalPlacementSettings = value ?? new CwcPlacementSettings();
        }

        /// <summary>
        /// 全局分帧性能时间预算（毫秒）
        /// </summary>
        public static float GlobalFrameBudgetMs
        {
            get => s_GlobalFrameBudgetMs;
            set => s_GlobalFrameBudgetMs = Mathf.Max(0.1f, value);
        }

        /// <summary>
        /// 地面碰撞层级代理属性
        /// </summary>
        public static LayerMask GroundLayer
        {
            get => s_GlobalPlacementSettings.GroundLayer;
            set => s_GlobalPlacementSettings.GroundLayer = value;
        }

        /// <summary>
        /// 墙体物理碰撞层级代理属性（专用于 WallSnapped 靠墙吸附与开阔地探测）
        /// </summary>
        public static LayerMask WallLayer
        {
            get => s_GlobalPlacementSettings.WallLayer;
            set => s_GlobalPlacementSettings.WallLayer = value;
        }

        /// <summary>
        /// 障碍物与实体碰撞层级代理属性
        /// </summary>
        public static LayerMask ObstacleLayer
        {
            get => s_GlobalPlacementSettings.ObstacleLayer;
            set => s_GlobalPlacementSettings.ObstacleLayer = value;
        }

        /// <summary>
        /// 是否启用 NavMesh 校验代理属性
        /// </summary>
        public static bool UseNavMesh
        {
            get => s_GlobalPlacementSettings.UseNavMesh;
            set => s_GlobalPlacementSettings.UseNavMesh = value;
        }

        #endregion

        #region 公开工具方法 (Public Methods)

        /// <summary>
        /// 获取有效生效的物理设置（支持局部覆盖回退到全局）
        /// </summary>
        public static CwcPlacementSettings GetEffectivePlacementSettings(CwcPlacementSettings overrideSettings = null)
        {
            return overrideSettings ?? s_GlobalPlacementSettings;
        }

        #endregion
    }
}
