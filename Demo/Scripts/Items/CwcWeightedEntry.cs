using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 加权单次摆放数学调度外壳抽象基类
    /// 包含宏观切蛋糕式的配额数学规则（权重、保底、封顶），支持无缝派生与通用编辑器绘制
    /// </summary>
    [Serializable]
    public abstract class CwcWeightedEntryBase
    {
        #region Inspector 序列化字段

        [Tooltip("Quota weight ratio")]
        [Min(1)]
        [SerializeField] protected int _weight = 10;

        [Tooltip("Minimum guaranteed count (0 for none)")]
        [Min(0)]
        [SerializeField] protected int _minLimit = 0;

        [Tooltip("Maximum allowed count (0 for none)")]
        [Min(0)]
        [SerializeField] protected int _maxLimit = 0;

        #endregion

        #region 公开属性

        public int Weight
        {
            get => _weight;
            set => _weight = Mathf.Max(1, value);
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

        #endregion

        #region 构造函数

        protected CwcWeightedEntryBase()
        {
        }

        protected CwcWeightedEntryBase(int weight = 10, int minLimit = 0, int maxLimit = 0)
        {
            _weight = Mathf.Max(1, weight);
            _minLimit = Mathf.Max(0, minLimit);
            _maxLimit = Mathf.Max(0, maxLimit);
        }

        #endregion

        #region 契约方法

        public abstract CwcPlacementItemBase ToPlacementItem();

        #endregion
    }

    /// <summary>
    /// 加权单次摆放数学调度外壳容器（纯 C# 类）
    /// 外壳只关心切蛋糕式的宏观数学规则，空间物理策略与激活流水线完全由 Payload 自描述
    /// </summary>
    [Serializable]
    public class CwcWeightedEntry<TPayload> : CwcWeightedEntryBase where TPayload : ISpawnPayload
    {
        #region Inspector 序列化字段

        [Tooltip("Payload content")]
        [SerializeField] private TPayload _payload;

        #endregion

        #region 公开属性

        public TPayload Payload
        {
            get => _payload;
            set => _payload = value;
        }

        #endregion

        #region 构造函数

        public CwcWeightedEntry()
        {
        }

        public CwcWeightedEntry(TPayload payload, int weight = 10, int minLimit = 0, int maxLimit = 0)
            : base(weight, minLimit, maxLimit)
        {
            _payload = payload;
        }

        public CwcWeightedEntry(TPayload payload, int weight, IPlacementStrategy strategy, int minLimit = 0, int maxLimit = 0)
            : base(weight, minLimit, maxLimit)
        {
            _payload = payload;
            if (payload is GameObjectPayload goPayload)
            {
                goPayload.Strategy = strategy;
            }
        }

        public CwcWeightedEntry(TPayload payload, int weight, PlacementMode mode, int minLimit = 0, int maxLimit = 0)
            : base(weight, minLimit, maxLimit)
        {
            _payload = payload;
            if (payload is GameObjectPayload goPayload)
            {
                goPayload.Mode = mode;
            }
        }

        #endregion

        #region 公开适配转换方法

        /// <summary>
        /// 转换为插件原生调度项
        /// 自动将外壳的数学参数（Weight, MinLimit, MaxLimit）与 Payload 的自描述参数组装为框架调度项
        /// </summary>
        public override CwcPlacementItemBase ToPlacementItem()
        {
            return new WeightedPlacementItemAdapter(this);
        }

        #endregion

        #region 私有适配器实现

        private class WeightedPlacementItemAdapter : CwcPlacementItemBase
        {
            private readonly CwcWeightedEntry<TPayload> _entry;

            public WeightedPlacementItemAdapter(CwcWeightedEntry<TPayload> entry)
            {
                _entry = entry;
                Weight = entry.Weight;
                MinLimit = entry.MinLimit;
                MaxLimit = entry.MaxLimit;

                if (entry.Payload != null)
                {
                    Strategy = entry.Payload.Strategy;
                    FootprintRadius = entry.Payload.FootprintRadius;
                    SpreadRadius = entry.Payload.SpreadRadius;
                    Count = entry.Payload.UnitCount;
                }
            }

            public override IEnumerator SpawnRoutine(Vector3 clusterCenter, IReadOnlyList<Pose> poses, CwcPlacementContext context)
            {
                if (_entry.Payload != null)
                {
                    yield return _entry.Payload.SpawnRoutine(poses, context);
                }
            }
        }

        #endregion
    }
}
