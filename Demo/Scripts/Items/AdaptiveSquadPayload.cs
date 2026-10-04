using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 极简敌群编制单兵种项非泛型基类
    /// 支持 Unity CustomPropertyDrawer 多态继承与通用反射绘制
    /// </summary>
    [Serializable]
    public abstract class SquadMemberEntryBase
    {
        #region Inspector 序列化字段

        [Tooltip("Unit count in squad")]
        [Min(1)]
        [SerializeField] protected int _count = 1;

        #endregion

        #region 公开属性

        public int Count
        {
            get => _count;
            set => _count = Mathf.Max(1, value);
        }

        public abstract int UnitCost { get; }

        #endregion
    }

    /// <summary>
    /// 极简敌群编制单兵种项（纯 C# 数据类）
    /// 仅需配置单位引用与数量，单体强度 Cost 自动从 ISpawnableUnit 提取
    /// </summary>
    [Serializable]
    public class SquadMemberEntry<TUnit> : SquadMemberEntryBase where TUnit : class, ISpawnableUnit
    {
        #region Inspector 序列化字段

        [Tooltip("Spawnable unit asset reference")]
        [SerializeField] private TUnit _unit;

        #endregion

        #region 公开属性

        public TUnit Unit
        {
            get => _unit;
            set => _unit = value;
        }

        public override int UnitCost => _unit != null ? _unit.BaseCost : 1;

        #endregion

        #region 构造函数

        public SquadMemberEntry()
        {
        }

        public SquadMemberEntry(TUnit unit, int count = 1)
        {
            _unit = unit;
            _count = Mathf.Max(1, count);
        }

        #endregion
    }

    /// <summary>
    /// 自适应敌群小队载体抽象基类
    /// 包含小队选点模式与散开半径，支持无缝派生与通用编辑器绘制
    /// </summary>
    [Serializable]
    public abstract class AdaptiveSquadPayloadBase : ICreditSpawnPayload
    {
        #region Inspector 序列化字段

        [Tooltip("Squad placement mode")]
        [SerializeField] private PlacementMode _mode = PlacementMode.Free;

        [Tooltip("Spread radius in meters (0 for auto)")]
        [SerializeField] private float _spreadRadius = 4f;

        #endregion

        #region 私有非序列化字段

        private IPlacementStrategy _strategy;

        #endregion

        #region 公开属性与契约实现

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

        public float SpreadRadius
        {
            get
            {
                if (_spreadRadius > 0.1f) return _spreadRadius;
                return Mathf.Max(3f, Mathf.Sqrt(UnitCount) * 1.5f);
            }
            set => _spreadRadius = Mathf.Max(0f, value);
        }

        public abstract int UnitCount { get; }
        public abstract float FootprintRadius { get; }
        public abstract int TotalCost { get; }

        /// <summary>
        /// 编辑器信息预览自描述契约（开闭原则，支持子类多态扩展）
        /// </summary>
        public virtual string SummaryText => $"Cost: {TotalCost}  ({UnitCount})";

        public abstract IEnumerator SpawnRoutine(IReadOnlyList<Pose> poses, CwcPlacementContext context);

        #endregion
    }

    /// <summary>
    /// 通用自适应敌群小队载体（纯 C# 类）
    /// 彻底自描述自身选点模式、散开半径与总战力消耗（TotalCost 自动从各单体 BaseCost 累加，外壳无冗余）
    /// 生成时按单体强度降序排列，强度最高者自然居中，较弱者葵花环绕展开
    /// </summary>
    [Serializable]
    public class AdaptiveSquadPayload<TUnit> : AdaptiveSquadPayloadBase where TUnit : class, ISpawnableUnit
    {
        #region Inspector 序列化字段

        [Tooltip("Squad member roster")]
        [SerializeField] private List<SquadMemberEntry<TUnit>> _members = new List<SquadMemberEntry<TUnit>>();

        #endregion

        #region 私有非序列化字段与内部结构

        private struct FlatOrder
        {
            public GameObject Prefab;
            public int UnitCost;
        }

        private readonly List<FlatOrder> _flatOrders = new List<FlatOrder>(16);

        #endregion

        #region 公开属性与契约实现

        public IReadOnlyList<SquadMemberEntry<TUnit>> Members => _members;

        /// <summary>
        /// 编辑器信息预览自描述契约（开闭原则）
        /// </summary>
        public override string SummaryText
        {
            get
            {
                if (_members == null || _members.Count == 0)
                {
                    return "Cost: 0  (0)";
                }

                int totalUnits = UnitCount;
                int totalCost = TotalCost;

                string modeBadge = Mode == PlacementMode.WallSnapped ? "  [Wall Snapped]" :
                                   Mode == PlacementMode.OpenCenter ? "  [Open Center]" : string.Empty;

                if (_members.Count == 1)
                {
                    return $"Cost: {totalCost}  ({totalUnits}){modeBadge}";
                }

                var counts = new List<int>(_members.Count);
                for (int i = 0; i < _members.Count; i++)
                {
                    counts.Add(_members[i] != null ? _members[i].Count : 1);
                }
                string ratioStr = string.Join(":", counts);

                return $"Cost: {totalCost}  ({totalUnits})  {ratioStr}{modeBadge}";
            }
        }

        /// <summary>
        /// 物体物理底盘占用半径（米）
        /// </summary>
        public override float FootprintRadius
        {
            get
            {
                float maxRadius = 0.5f;
                if (_members != null)
                {
                    int memberCount = _members.Count;
                    for (int i = 0; i < memberCount; i++)
                    {
                        var entry = _members[i];
                        if (entry != null && entry.Unit != null && entry.Unit.Prefab != null)
                        {
                            float r = CwcPlacementSpatialUtil.GetPhysicalFootprintRadius(entry.Unit.Prefab);
                            if (r > maxRadius) maxRadius = r;
                        }
                    }
                }
                return maxRadius;
            }
        }

        public override int UnitCount
        {
            get
            {
                int total = 0;
                if (_members != null)
                {
                    int memberCount = _members.Count;
                    for (int i = 0; i < memberCount; i++)
                    {
                        var entry = _members[i];
                        if (entry != null)
                        {
                            total += entry.Count;
                        }
                    }
                }
                return Mathf.Max(1, total);
            }
        }

        public override int TotalCost
        {
            get
            {
                if (_members == null || _members.Count == 0) return 1;

                int sum = 0;
                int count = _members.Count;
                for (int i = 0; i < count; i++)
                {
                    var entry = _members[i];
                    if (entry != null && entry.Unit != null)
                    {
                        sum += entry.Unit.BaseCost * entry.Count;
                    }
                }

                return Mathf.Max(1, sum);
            }
        }

        #endregion

        #region 构造函数

        public AdaptiveSquadPayload()
        {
        }

        public AdaptiveSquadPayload(List<SquadMemberEntry<TUnit>> members, float spreadRadius = 4f, IPlacementStrategy strategy = null)
        {
            _members = members ?? new List<SquadMemberEntry<TUnit>>();
            SpreadRadius = spreadRadius;
            Strategy = strategy;
        }

        public AdaptiveSquadPayload(List<SquadMemberEntry<TUnit>> members, float spreadRadius, PlacementMode mode)
        {
            _members = members ?? new List<SquadMemberEntry<TUnit>>();
            SpreadRadius = spreadRadius;
            Mode = mode;
        }

        #endregion

        #region ICreditSpawnPayload 契约实现

        public override IEnumerator SpawnRoutine(IReadOnlyList<Pose> poses, CwcPlacementContext context)
        {
            if (_members == null || _members.Count == 0 || poses == null || poses.Count == 0)
            {
                yield break;
            }

            // 1. 展平小队各单体
            _flatOrders.Clear();
            int memberCount = _members.Count;
            for (int m = 0; m < memberCount; m++)
            {
                var entry = _members[m];
                if (entry == null || entry.Unit == null || entry.Unit.Prefab == null) continue;

                int count = entry.Count;
                for (int c = 0; c < count; c++)
                {
                    _flatOrders.Add(new FlatOrder
                    {
                        Prefab = entry.Unit.Prefab,
                        UnitCost = entry.Unit.BaseCost
                    });
                }
            }

            // 2. 核心数学自适应：按单体强度降序排列（最高者排在首位）
            _flatOrders.Sort((a, b) => b.UnitCost.CompareTo(a.UnitCost));

            // 3. 依序出池：强度最高的自动获取 poses[0]（中心落点），较弱者葵花点阵环绕四周
            int spawnLimit = Mathf.Min(_flatOrders.Count, poses.Count);
            for (int i = 0; i < spawnLimit; i++)
            {
                var order = _flatOrders[i];
                Pose targetPose = poses[i];

                var hook = context.EntityManager.Get(order.Prefab, context.OwnerModule);
                if (hook != null)
                {
                    hook.transform.position = targetPose.position;
                    hook.transform.rotation = targetPose.rotation;
                    hook.ThreatCost = order.UnitCost;
                    hook.gameObject.SetActive(true);
                }

                if (context.ShouldYield)
                {
                    yield return null;
                    context.ResetFrameTimer();
                }
            }
        }

        #endregion

        #region 公开操作方法

        /// <summary>
        /// 添加编制成员
        /// </summary>
        public void AddMember(TUnit unit, int count = 1)
        {
            if (unit != null)
            {
                _members.Add(new SquadMemberEntry<TUnit>(unit, count));
            }
        }

        #endregion
    }
}
