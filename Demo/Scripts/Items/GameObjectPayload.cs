using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 开箱即用的场景交互物预制体载体（纯 C# 类）
    /// 专用于宝箱、神龛、资源矿石等中立静态物件的单次加权摆放
    /// 天然单体（数量恒为 1），无需散开、无需池化管理，直接在场景默认容器中实例化
    /// </summary>
    [Serializable]
    public class GameObjectPayload : ISpawnPayload
    {
        #region 常量与静态 (Constants & Static)

        private const string DefaultContainerName = "[Interactables]";

        #endregion

        #region Inspector 序列化字段 (Serialized Fields)

        [Tooltip("Target interactable prefab")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Placement mode (Free / BackToWall / Clearing)")]
        [SerializeField] private PlacementMode _mode = PlacementMode.Free;

        #endregion

        #region 私有非序列化字段 (Private Fields)

        private IPlacementStrategy _strategy;

        #endregion

        #region 公开属性与契约实现 (Public Properties)

        public GameObject Prefab
        {
            get => _prefab;
            set => _prefab = value;
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

        /// <summary>
        /// 交互物单点放置数量恒定为 1
        /// </summary>
        public int UnitCount => 1;

        /// <summary>
        /// 交互物单体无需散开，半径恒定为 0
        /// </summary>
        public float SpreadRadius => 0f;

        /// <summary>
        /// 多态空间放置策略（支持接口注入或由序列化模式解析）
        /// </summary>
        public IPlacementStrategy Strategy
        {
            get => _strategy ?? CwcPlacementStrategies.FromMode(_mode);
            set => _strategy = value;
        }

        /// <summary>
        /// 物体物理底盘占用半径（米）
        /// 纯原生从预制体碰撞体自动提取，预制体无需挂载任何专有脚本或实现接口
        /// </summary>
        public float FootprintRadius => CwcPlacementSpatialUtil.GetPhysicalFootprintRadius(_prefab);

        /// <summary>
        /// 交互物战力消耗严格为 0，不占任何遭遇战预算
        /// </summary>
        public int TotalCost => 0;

        /// <summary>
        /// 编辑器信息预览自描述契约（开闭原则）
        /// </summary>
        public virtual string SummaryText
        {
            get
            {
                string name = _prefab != null ? _prefab.name : "(None)";
                string modeBadge = _mode == PlacementMode.WallSnapped ? "  [Wall Snapped]" :
                                   _mode == PlacementMode.OpenCenter ? "  [Open Center]" : string.Empty;
                return $"{name}{modeBadge}";
            }
        }

        #endregion

        #region 构造函数 (Constructors)

        public GameObjectPayload()
        {
        }

        public GameObjectPayload(
            GameObject prefab,
            PlacementMode mode = PlacementMode.Free,
            IPlacementStrategy strategy = null)
        {
            _prefab = prefab;
            _mode = mode;
            _strategy = strategy ?? CwcPlacementStrategies.FromMode(mode);
        }

        #endregion

        #region 契约执行方法 (Public Methods)

        public IEnumerator SpawnRoutine(IReadOnlyList<Pose> poses, CwcPlacementContext context)
        {
            if (_prefab == null || poses == null) yield break;

            int poseCount = poses.Count;
            Transform parentContainer = GetOrCreateContainer();

            for (int i = 0; i < poseCount; i++)
            {
                Pose pose = poses[i];

                // 交互物直接在默认容器下实例化（不入对象池、不占战力、不触发超距淘汰）
                GameObject instance = UnityEngine.Object.Instantiate(_prefab, pose.position, pose.rotation, parentContainer);

                // 若预制体上偶然残留挂载了 CwcSceneEntityHook，立即安全移除，杜绝其自动向实体管理器报到引发战力与淘汰污染
                if (instance.TryGetComponent<CwcSceneEntityHook>(out var hook))
                {
                    UnityEngine.Object.Destroy(hook);
                }

                // 分帧耗时保护
                if (context.ShouldYield)
                {
                    yield return null;
                    context.ResetFrameTimer();
                }
            }
        }

        #endregion

        #region 私有辅助方法 (Private Methods)

        private static Transform GetOrCreateContainer()
        {
            GameObject containerGo = GameObject.Find(DefaultContainerName);
            if (containerGo == null)
            {
                containerGo = new GameObject(DefaultContainerName);
            }
            return containerGo.transform;
        }

        #endregion
    }
}
