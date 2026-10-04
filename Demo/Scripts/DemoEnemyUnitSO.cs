using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 演示用敌人单位配置资产 (ScriptableObject)
    /// 实现 ISpawnableUnit 契约，供自适应小队 (AdaptiveSquadPayload) 直接引用
    /// </summary>
    [CreateAssetMenu(fileName = "DemoEnemyUnit", menuName = "Cwcbb/Demo/Scene Director/Demo Enemy Unit")]
    public class DemoEnemyUnitSO : ScriptableObject, ISpawnableUnit
    {
        #region Inspector 序列化字段

        [Tooltip("Unit name")]
        [SerializeField] private string _unitName = "Demo Enemy";

        [Tooltip("Enemy entity prefab")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Base threat cost")]
        [Min(1)]
        [SerializeField] private int _baseCost = 2;

        #endregion

        #region 公开属性与 ISpawnableUnit 契约实现

        public string UnitName => _unitName;

        public GameObject Prefab => _prefab;

        public int BaseCost => Mathf.Max(1, _baseCost);

        #endregion
    }
}
