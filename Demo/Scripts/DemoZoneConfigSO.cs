using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 演示用关卡/区域生成配置资产 (ScriptableObject)
    /// 演示如何在自定义 SO 内部直接嵌入纯 C# 配置类字段，实现完全数据驱动的资产化管理
    /// </summary>
    [CreateAssetMenu(fileName = "DemoZoneConfig", menuName = "Cwcbb/Demo/Scene Director/Demo Zone Config")]
    public class DemoZoneConfigSO : ScriptableObject
    {
        #region Inspector 序列化字段

        [Tooltip("Interactable placement configuration")]
        [SerializeField]
        private WeightedPlacementConfig<GameObjectPayload> _interactableConfig = new WeightedPlacementConfig<GameObjectPayload>();

        [Tooltip("Enemy encounter dynamic director configuration")]
        [SerializeField]
        private CreditDirectorConfig<AdaptiveSquadPayload<DemoEnemyUnitSO>> _encounterConfig = new CreditDirectorConfig<AdaptiveSquadPayload<DemoEnemyUnitSO>>();

        #endregion

        #region 公开属性

        public WeightedPlacementConfig<GameObjectPayload> InteractableConfig => _interactableConfig;

        public CreditDirectorConfig<AdaptiveSquadPayload<DemoEnemyUnitSO>> EncounterConfig => _encounterConfig;

        #endregion
    }
}
