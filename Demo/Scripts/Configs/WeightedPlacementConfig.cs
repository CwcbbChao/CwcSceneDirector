using System;
using System.Collections.Generic;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 加权单次放置通用泛型配置类（纯 C# 数据类）
    /// 纯粹专注于组织“要生成什么”（各项放置物及其出现权重比例）
    /// 物理规则统一由全局 CwcSceneDirectorSettings 维护，空间总范围与生成规格由激活时的 PlacementAreaContext 传入
    /// </summary>
    [Serializable]
    public class WeightedPlacementConfig<TPayload> : IWeightedPlacementConfig where TPayload : ISpawnPayload
    {
        #region Inspector 序列化字段

        [Tooltip("Weighted placement item roster")]
        [SerializeField] private List<CwcWeightedEntry<TPayload>> _items = new List<CwcWeightedEntry<TPayload>>();

        #endregion

        #region 公开属性与接口实现

        public IReadOnlyList<CwcWeightedEntry<TPayload>> Items => _items;

        #endregion

        #region 构造函数

        public WeightedPlacementConfig()
        {
        }

        public WeightedPlacementConfig(List<CwcWeightedEntry<TPayload>> items)
        {
            _items = items ?? new List<CwcWeightedEntry<TPayload>>();
        }

        #endregion

        #region 核心契约方法 (类型擦除转化为框架调度项)

        /// <summary>
        /// 将自身强类型列表填充并转换为原生调度项
        /// </summary>
        public void PopulatePlacementItems(List<CwcPlacementItemBase> outputList)
        {
            if (outputList == null || _items == null) return;

            int count = _items.Count;
            for (int i = 0; i < count; i++)
            {
                var entry = _items[i];
                if (entry != null)
                {
                    outputList.Add(entry.ToPlacementItem());
                }
            }
        }

        #endregion

        #region 工厂构建方法

        /// <summary>
        /// 结合调用方传入的运行时空间上下文，构建可运行的加权放置模块
        /// 物理设置自动从全局 CwcSceneDirectorSettings 读取（支持上下文局部覆盖）
        /// </summary>
        public CwcWeightedPlacementModule CreateModule(PlacementAreaContext context)
        {
            if (context == null)
            {
                context = new PlacementAreaContext(Vector3.zero);
            }

            var settings = CwcSceneDirectorSettings.GetEffectivePlacementSettings(context.SettingsOverride);

            var module = new CwcWeightedPlacementModule(
                context.Center,
                context.Radius,
                context.MinClusterDistance,
                context.MaxClusters,
                settings,
                context.UseSpatialGrid
            );

            module.SetFrameBudgetMs(CwcSceneDirectorSettings.GlobalFrameBudgetMs);

            var placementItems = new List<CwcPlacementItemBase>(_items.Count);
            PopulatePlacementItems(placementItems);

            int itemCount = placementItems.Count;
            for (int i = 0; i < itemCount; i++)
            {
                module.AddItem(placementItems[i]);
            }

            return module;
        }

        /// <summary>
        /// 便捷构建重载：直接传入中心坐标与半径
        /// </summary>
        public CwcWeightedPlacementModule CreateModule(
            Vector3 center,
            float radius = 25f,
            int maxClusters = 8,
            float minClusterDistance = 6f)
        {
            var context = new PlacementAreaContext(center, radius, maxClusters, minClusterDistance);
            return CreateModule(context);
        }

        /// <summary>
        /// 添加条目
        /// </summary>
        public void AddEntry(CwcWeightedEntry<TPayload> entry)
        {
            if (entry != null)
            {
                _items.Add(entry);
            }
        }

        #endregion
    }
}
