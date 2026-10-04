using System;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 带信用点消耗（Cost）的动态刷怪放置项多态基类
    /// 继承自 CwcPlacementItemBase，天然具备位姿计算、放置模式（自由/靠墙/开阔）与微观散开能力
    /// </summary>
    [Serializable]
    public abstract class CwcCreditPlacementItemBase : CwcPlacementItemBase
    {
        #region 私有非序列化字段 (Private Fields)

        private int _runtimeThreatCost = 1;

        #endregion

        #region 公开属性 (Public Properties)

        /// <summary>
        /// 实体战力威胁度信用点消耗（Threat Cost，单一真实数据源，优先由实体/载体自描述）
        /// </summary>
        public override int ThreatCost
        {
            get => _runtimeThreatCost;
            set => _runtimeThreatCost = Mathf.Max(1, value);
        }

        /// <summary>
        /// 兼容旧调用的 Cost 别名访问器
        /// </summary>
        public int Cost
        {
            get => ThreatCost;
            set => ThreatCost = value;
        }

        #endregion

        #region 构造函数 (Constructors)

        protected CwcCreditPlacementItemBase()
        {
        }

        protected CwcCreditPlacementItemBase(int cost, int weight = 10, int count = 1, PlacementMode mode = PlacementMode.Free)
        {
            _runtimeThreatCost = Mathf.Max(1, cost);
            Weight = weight;
            Count = count;
            Mode = mode;
        }

        #endregion
    }

    /// <summary>
    /// 泛型带消耗放置项基类
    /// 支持强类型绑定任意自定义敌群配置资产（如 EnemySpawnConfigSO、自定义数据类等）
    /// </summary>
    [Serializable]
    public abstract class CwcCreditPlacementItem<TData> : CwcCreditPlacementItemBase
    {
        [Tooltip("Target configuration data")]
        [SerializeField] private TData _data;

        public TData Data
        {
            get => _data;
            set => _data = value;
        }

        protected CwcCreditPlacementItem()
        {
        }

        protected CwcCreditPlacementItem(TData data, int cost, int weight = 10, int count = 1, PlacementMode mode = PlacementMode.Free)
            : base(cost, weight, count, mode)
        {
            _data = data;
        }
    }

    /// <summary>
    /// 开箱即用的预制体信用点放置项
    /// 直接针对单个或多个敌人预制体，生成时自动设置位姿与 ThreatCost，开箱即用
    /// </summary>
    [Serializable]
    public class CwcCreditPrefabPlacementItem : CwcCreditPlacementItem<GameObject>
    {
        public CwcCreditPrefabPlacementItem()
        {
        }

        public CwcCreditPrefabPlacementItem(
            GameObject prefab,
            int cost,
            int weight = 10,
            int count = 1,
            PlacementMode mode = PlacementMode.Free)
            : base(prefab, cost, weight, count, mode)
        {
        }

        public override System.Collections.IEnumerator SpawnRoutine(
            Vector3 clusterCenter,
            System.Collections.Generic.IReadOnlyList<Pose> poses,
            CwcPlacementContext context)
        {
            if (Data == null) yield break;

            int count = poses.Count;
            // 单只怪平均分配该项的 Cost（至少为 1）
            int unitCost = Mathf.Max(1, Cost / Mathf.Max(1, count));

            for (int i = 0; i < count; i++)
            {
                Pose pose = poses[i];
                var hook = context.EntityManager.Get(Data, context.OwnerModule);
                if (hook != null)
                {
                    hook.transform.position = pose.position;
                    hook.transform.rotation = pose.rotation;
                    hook.ThreatCost = unitCost;
                    hook.gameObject.SetActive(true);
                }

                // 仅当单帧耗时超出预算时才让出主线程
                if (context.ShouldYield)
                {
                    yield return null;
                    context.ResetFrameTimer();
                }
            }
        }
    }
}
