using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 场景实体身份外壳与生命周期钩子组件
    /// 挂载在实体根物体上，记录开销与分类标签，并利用 OnEnable/OnDisable 自动完成向管理器的报到与归池注销
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Cwcbb/Scene Director/Cwc Scene Entity Hook")]
    public class CwcSceneEntityHook : MonoBehaviour
    {
        #region 常量与静态成员 (Constants & Static)

        private const string LogPrefix = "[CwcSceneEntityHook] ";

        #endregion

        #region Inspector 序列化字段 (Serialized Fields)

        [Header("实体属性 (Entity Settings)")]
        [Tooltip("单位占用的资源开销/威胁度点数，用于动态刷怪导演预算计算")]
        [SerializeField] private int _threatCost = 1;

        [Tooltip("是否允许被超距自动淘汰回收。若为 false（如 Boss 或关键精英），即便超出玩家距离也会持久保留")]
        [SerializeField] private bool _allowCulling = true;

        #endregion

        #region 私有非序列化字段 (Private Fields)

        private GameObject _prefabSource;
        private object _creator;
        private int _defaultThreatCost = 1;
        private bool _defaultAllowCulling = true;
        private float _spawnTimestamp;
        private Vector2Int _gridCellCoord = new Vector2Int(int.MinValue, int.MinValue);
        private bool _isRegistered = false;

        #endregion

        #region 公开属性 (Public Properties)

        /// <summary>
        /// 实体威胁度/资源开销
        /// </summary>
        public int ThreatCost
        {
            get => _threatCost;
            set => _threatCost = Mathf.Max(0, value);
        }

        /// <summary>
        /// 是否允许被超距淘汰回收
        /// </summary>
        public bool AllowCulling
        {
            get => _allowCulling;
            set => _allowCulling = value;
        }

        /// <summary>
        /// 产生此实体的预制体模板源引用（用于自动寻池归位）
        /// </summary>
        public GameObject PrefabSource => _prefabSource;

        /// <summary>
        /// 创建并出资此实体的来源模块/对象（供实体管理器自动扣费与审计）
        /// </summary>
        public object Creator
        {
            get => _creator;
            set => _creator = value;
        }

        /// <summary>
        /// 实体本次激活出池的时间戳
        /// </summary>
        public float SpawnTimestamp => _spawnTimestamp;

        /// <summary>
        /// 实体当前在空间网格中的格坐标
        /// </summary>
        public Vector2Int GridCellCoord => _gridCellCoord;

        /// <summary>
        /// 当前是否已在单位管理器中处于活跃注册状态
        /// </summary>
        public bool IsRegistered => _isRegistered;

        #endregion

        #region Unity 生命周期 (Unity Lifecycle)

        private void OnEnable()
        {
            // 当出池请求者完成属性修饰并调用 SetActive(true) 后，Unity 触发 OnEnable，此时数据完整，自动报到
            if (CwcSceneEntityManager.HasInstance || CwcSceneEntityManager.CanLazyCreate)
            {
                _spawnTimestamp = Time.time;
                _isRegistered = true;
                CwcSceneEntityManager.Instance.RegisterActiveEntity(this);
            }
        }

        private void OnDisable()
        {
            // 守卫：如果游戏退出或场景正在卸载，不执行回池
            if (CwcSceneEntityManager.IsApplicationQuitting)
            {
                _isRegistered = false;
                return;
            }

            if (_isRegistered && CwcSceneEntityManager.HasInstance)
            {
                _isRegistered = false;
                CwcSceneEntityManager.Instance.UnregisterAndRecycle(this);
            }
        }

        private void OnDestroy()
        {
            if (_isRegistered && CwcSceneEntityManager.HasInstance && !CwcSceneEntityManager.IsApplicationQuitting)
            {
                _isRegistered = false;
                CwcSceneEntityManager.Instance.UnregisterDestroyedEntity(this);
            }
        }

        #endregion

        #region 公开配置与控制方法 (Public Methods)

        /// <summary>
        /// 首次动态挂载或实例化时初始化基准配置与来源标记
        /// </summary>
        public void Initialize(GameObject prefabSource)
        {
            _prefabSource = prefabSource;
            _defaultThreatCost = _threatCost;
            _defaultAllowCulling = _allowCulling;
        }

        /// <summary>
        /// 对象从池中复用取出时，重置运行时状态为基准默认值，防止上一轮业务修改污染
        /// </summary>
        public void ResetRuntimeState()
        {
            _threatCost = _defaultThreatCost;
            _allowCulling = _defaultAllowCulling;
            _creator = null;
            _isRegistered = false;
            _gridCellCoord = new Vector2Int(int.MinValue, int.MinValue);
        }

        /// <summary>
        /// 内部设置当前记录的空间网格坐标
        /// </summary>
        public void SetGridCellCoord(Vector2Int coord)
        {
            _gridCellCoord = coord;
        }

        /// <summary>
        /// 主动回收/淘汰实体（直接禁用自身，触发 OnDisable 自动回池链路）
        /// </summary>
        public void Despawn()
        {
            gameObject.SetActive(false);
        }

        #endregion
    }
}
