using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 动态信用点刷怪调度外壳抽象基类
    /// 包含单次抽选概率权重，支持无缝派生与通用编辑器绘制
    /// </summary>
    [Serializable]
    public abstract class CwcCreditEntryBase
    {
        #region Inspector 序列化字段

        [Tooltip("Weight ratio")]
        [Min(1)]
        [SerializeField] protected int _weight = 10;

        #endregion

        #region 公开属性

        public int Weight
        {
            get => _weight;
            set => _weight = Mathf.Max(1, value);
        }

        #endregion

        #region 构造函数

        protected CwcCreditEntryBase()
        {
        }

        protected CwcCreditEntryBase(int weight = 10)
        {
            _weight = Mathf.Max(1, weight);
        }

        #endregion

        #region 契约方法

        public abstract CwcCreditPlacementItemBase ToCreditPlacementItem();

        #endregion
    }

    /// <summary>
    /// 动态信用点刷怪数学调度外壳容器（纯 C# 类）
    /// 外壳只关心抽选概率，战力消耗完全由 Payload 内部真实单位自描述
    /// </summary>
    [Serializable]
    public class CwcCreditEntry<TPayload> : CwcCreditEntryBase where TPayload : ICreditSpawnPayload
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

        public CwcCreditEntry()
        {
        }

        public CwcCreditEntry(TPayload payload, int weight = 10)
            : base(weight)
        {
            _payload = payload;
        }

        public CwcCreditEntry(TPayload payload, int weight, IPlacementStrategy strategy, float spreadRadius = 4f)
            : this(payload, weight)
        {
            if (payload is AdaptiveSquadPayload<DemoEnemyUnitSO> squadPayload)
            {
                squadPayload.Strategy = strategy;
                squadPayload.SpreadRadius = spreadRadius;
            }
        }

        public CwcCreditEntry(TPayload payload, int weight, int baseCost, PlacementMode mode = PlacementMode.Free, float spreadRadius = 4f)
            : this(payload, weight)
        {
            if (payload is AdaptiveSquadPayload<DemoEnemyUnitSO> squadPayload)
            {
                squadPayload.Mode = mode;
                squadPayload.SpreadRadius = spreadRadius;
            }
        }

        #endregion

        #region 公开适配转换方法

        /// <summary>
        /// 转换为插件原生信用点调度项
        /// 战力开销（ThreatCost）直接由 Payload.TotalCost 动态提供，单一真实数据源
        /// </summary>
        public override CwcCreditPlacementItemBase ToCreditPlacementItem()
        {
            return new CreditDirectorItemAdapter(this);
        }

        #endregion

        #region 私有适配器实现

        private class CreditDirectorItemAdapter : CwcCreditPlacementItemBase
        {
            private readonly CwcCreditEntry<TPayload> _entry;

            public CreditDirectorItemAdapter(CwcCreditEntry<TPayload> entry)
            {
                _entry = entry;
                Weight = entry.Weight;

                if (entry.Payload != null)
                {
                    Strategy = entry.Payload.Strategy;
                    FootprintRadius = entry.Payload.FootprintRadius;
                    SpreadRadius = entry.Payload.SpreadRadius;
                    Count = entry.Payload.UnitCount;
                }
            }

            /// <summary>
            /// 动态向 Payload 索要真实总战力消耗，单一真实数据源
            /// </summary>
            public override int ThreatCost
            {
                get => _entry.Payload != null ? _entry.Payload.TotalCost : 1;
                set { }
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
