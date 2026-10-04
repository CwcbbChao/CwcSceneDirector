using System;
using System.Collections.Generic;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 动态信用点导演通用泛型配置类（纯 C# 数据类）
    /// 纯粹专注于组织“要生成什么”（敌群各小队编制、抽取权重与单体消耗）
    /// 物理规则统一由全局 CwcSceneDirectorSettings 维护，遭遇战总范围、战力预算与节奏由激活时的 EncounterContext 传入
    /// </summary>
    [Serializable]
    public class CreditDirectorConfig<TPayload> : ICreditDirectorConfig where TPayload : ICreditSpawnPayload
    {
        #region Inspector 序列化字段

        [Tooltip("Encounter squad item roster")]
        [SerializeField] private List<CwcCreditEntry<TPayload>> _items = new List<CwcCreditEntry<TPayload>>();

        #endregion

        #region 公开属性与接口实现

        public IReadOnlyList<CwcCreditEntry<TPayload>> Items => _items;

        #endregion

        #region 构造函数

        public CreditDirectorConfig()
        {
        }

        public CreditDirectorConfig(List<CwcCreditEntry<TPayload>> items)
        {
            _items = items ?? new List<CwcCreditEntry<TPayload>>();
        }

        #endregion

        #region 核心契约方法 (类型擦除转化为框架调度项)

        /// <summary>
        /// 将自身强类型列表填充并转换为原生调度项
        /// </summary>
        public void PopulateCreditItems(List<CwcCreditPlacementItemBase> outputList)
        {
            if (outputList == null || _items == null) return;

            int count = _items.Count;
            for (int i = 0; i < count; i++)
            {
                var entry = _items[i];
                if (entry != null)
                {
                    outputList.Add(entry.ToCreditPlacementItem());
                }
            }
        }

        #endregion

        #region 工厂构建方法

        /// <summary>
        /// 结合调用方传入的运行时遭遇战上下文，构建可运行的动态信用点导演模块
        /// 物理设置自动从全局 CwcSceneDirectorSettings 读取（支持上下文局部覆盖）
        /// </summary>
        public CwcCreditDirectorModule CreateModule(EncounterContext context)
        {
            if (context == null)
            {
                context = new EncounterContext(Vector3.zero);
            }

            var settings = CwcSceneDirectorSettings.GetEffectivePlacementSettings(context.SettingsOverride);

            var module = new CwcCreditDirectorModule(
                context.Center,
                context.Radius,
                context.TotalBudget,
                context.MaxConcurrentCost,
                context.MinWaveCost,
                settings,
                context.MinRadius,
                context.CenterTarget
            );

            module.WaveCooldown = context.WaveCooldown;
            module.EvaluationInterval = context.EvaluationInterval;

            var creditItems = new List<CwcCreditPlacementItemBase>(_items.Count);
            PopulateCreditItems(creditItems);

            int itemCount = creditItems.Count;
            for (int i = 0; i < itemCount; i++)
            {
                module.AddItem(creditItems[i]);
            }

            return module;
        }

        /// <summary>
        /// 便捷构建重载：直接传入中心坐标、半径与预算
        /// </summary>
        public CwcCreditDirectorModule CreateModule(
            Vector3 center,
            float radius = 22f,
            int totalBudget = 50,
            int maxConcurrentCost = 14,
            float waveCooldown = 3.5f,
            float minRadius = 8f,
            Transform centerTarget = null)
        {
            var context = new EncounterContext(center, radius, totalBudget, maxConcurrentCost, waveCooldown, null, minRadius, centerTarget);
            return CreateModule(context);
        }

        /// <summary>
        /// 添加条目
        /// </summary>
        public void AddEntry(CwcCreditEntry<TPayload> entry)
        {
            if (entry != null)
            {
                _items.Add(entry);
            }
        }

        #endregion
    }
}
