using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 场景单位管理器
    /// 负责实体对象池集中调度、空间网格分区感知、以及基于关注目标（玩家）的超距自动淘汰回收
    /// </summary>
    [AddComponentMenu("Cwcbb/Scene Director/Cwc Scene Entity Manager")]
    public class CwcSceneEntityManager : MonoBehaviour
    {
        #region 常量与静态成员 (Constants & Static)

        private const string LogPrefix = "[CwcSceneEntityManager] ";
        private const string DefaultGameObjectName = "[CwcSceneEntityManager]";
        private const string PoolContainerName = "--- CwcEntityPoolHolder ---";

        private static CwcSceneEntityManager _instance;
        private static bool _isApplicationQuitting = false;

        #endregion

        #region Inspector 序列化字段 (Serialized Fields)

        [Header("空间网格设置 (Spatial Grid Settings)")]
        [Tooltip("空间网格单元格大小（米）")]
        [SerializeField] private float _gridCellSize = 15f;

        [Tooltip("空间网格位置更新频率（秒）")]
        [SerializeField] private float _gridUpdateInterval = 0.2f;

        [Header("超距回收设置 (Cull & Despawn Settings)")]
        [Tooltip("是否启用超距自动淘汰回收")]
        [SerializeField] private bool _enableCull = true;

        [Tooltip("淘汰判定距离阈值（米），超出所有关注目标此距离则回收")]
        [SerializeField] private float _cullDistance = 60f;

        [Tooltip("淘汰保护缓冲期（秒），新出池单位在此时间内免疫超距淘汰")]
        [SerializeField] private float _cullGracePeriod = 5.0f;

        [Tooltip("超距轮询检测频率（秒）")]
        [SerializeField] private float _cullCheckInterval = 0.5f;

        [Tooltip("关注目标列表（如玩家 Transform）。为空时将尝试自动查找 Player 标签对象")]
        [SerializeField] private List<Transform> _focusTargets = new List<Transform>(4);

        #endregion

        #region 内部数据结构与私有字段 (Internal Structures & Private Fields)

        private class PrefabPoolNode
        {
            public GameObject Prefab;
            public Transform Root;
            public Stack<CwcSceneEntityHook> InactiveStack = new Stack<CwcSceneEntityHook>(16);
        }

        private readonly Dictionary<int, PrefabPoolNode> _pools = new Dictionary<int, PrefabPoolNode>(32);
        private readonly List<CwcSceneEntityHook> _activeEntities = new List<CwcSceneEntityHook>(128);

        private CwcSpatialGrid _spatialGrid;
        private Transform _poolContainer;

        private float _gridUpdateTimer = 0f;
        private float _cullCheckTimer = 0f;
        private int _totalThreatCost = 0;
        private bool _hasWarnedMissingFocusTarget = false;

        #endregion

        #region 公开事件与属性 (Events & Properties)

        /// <summary>
        /// 当实体激活并登记到管理器时触发
        /// </summary>
        public event Action<CwcSceneEntityHook> OnEntityRegistered;

        /// <summary>
        /// 当实体禁用并从管理器注销时触发
        /// </summary>
        public event Action<CwcSceneEntityHook> OnEntityUnregistered;

        /// <summary>
        /// 应用程序是否正在退出
        /// </summary>
        public static bool IsApplicationQuitting => _isApplicationQuitting;

        /// <summary>
        /// 是否允许懒创建
        /// </summary>
        public static bool CanLazyCreate => !_isApplicationQuitting;

        /// <summary>
        /// 单位管理器场景级单例访问点，场景中无实例时将自动懒创建独立节点
        /// </summary>
        public static CwcSceneEntityManager Instance
        {
            get
            {
                if (_isApplicationQuitting) return null;
                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _instance = FindFirstObjectByType<CwcSceneEntityManager>();
#else
                    _instance = FindObjectOfType<CwcSceneEntityManager>();
#endif
                    if (_instance == null && !_isApplicationQuitting)
                    {
                        GameObject managerGo = new GameObject(DefaultGameObjectName);
                        _instance = managerGo.AddComponent<CwcSceneEntityManager>();
                        Debug.Log(LogPrefix + "检测到当前场景未配置单位管理器节点，已自动创建 [" + DefaultGameObjectName + "] 实例。");
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 场景中是否存在单例
        /// </summary>
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// 当前场上活跃实体总数
        /// </summary>
        public int ActiveEntityCount => _activeEntities.Count;

        /// <summary>
        /// 当前场上活跃实体的总威胁开销（Threat Cost）
        /// </summary>
        public int TotalThreatCost => _totalThreatCost;

        /// <summary>
        /// 活跃实体只读列表
        /// </summary>
        public IReadOnlyList<CwcSceneEntityHook> ActiveEntities => _activeEntities;

        #endregion

        #region Unity 生命周期 (Unity Lifecycle)

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(LogPrefix + "检测到场景中存在重复的 CwcSceneEntityManager 实例，正在移除冗余组件：" + gameObject.name);
                Destroy(this);
                return;
            }

            _instance = this;
            _spatialGrid = new CwcSpatialGrid(_gridCellSize);
            InitPoolContainer();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. 定期更新空间网格中的实体位置
            _gridUpdateTimer += dt;
            if (_gridUpdateTimer >= _gridUpdateInterval)
            {
                _gridUpdateTimer = 0f;
                UpdateSpatialPositions();
            }

            // 2. 定期执行超距淘汰回收检测
            if (_enableCull)
            {
                _cullCheckTimer += dt;
                if (_cullCheckTimer >= _cullCheckInterval)
                {
                    _cullCheckTimer = 0f;
                    CheckCulling();
                }
            }
        }

        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            _spatialGrid?.Clear();
            _pools.Clear();
            _activeEntities.Clear();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion

        #region 公开对象池与生成接口 (Public Pool Methods)

        /// <summary>
        /// 从对象池获取实体外壳（保持未激活状态，交给出池请求者自由装配与激活）
        /// 实体实例统一归属在预制体分类专属父节点下，确保场景 Hierarchy 井井有条
        /// </summary>
        /// <param name="prefab">实体预制体</param>
        /// <param name="creator">申请创建此实体的来源模块/对象（供实体管理器自动扣费与审计）</param>
        public CwcSceneEntityHook Get(GameObject prefab, object creator = null)
        {
            if (prefab == null)
            {
                Debug.LogError(LogPrefix + "从对象池提取失败：预制体为 null！");
                return null;
            }

            PrefabPoolNode node = GetOrCreatePoolNode(prefab);
            CwcSceneEntityHook hook = null;

            while (node.InactiveStack.Count > 0)
            {
                var candidate = node.InactiveStack.Pop();
                if (candidate != null)
                {
                    hook = candidate;
                    break;
                }
            }

            if (hook == null)
            {
                GameObject instance = Instantiate(prefab, node.Root);
                instance.SetActive(false);

                // 优先尝试获取预制件已有组件，若无则动态挂载，确保全局有且仅有一个 Hook
                if (!instance.TryGetComponent(out hook))
                {
                    hook = instance.AddComponent<CwcSceneEntityHook>();
                }

                // 首次动态挂载初始化基准状态
                hook.Initialize(prefab);
            }
            else
            {
                // 从池中复用取出时，重置运行时状态为基准默认值，防止上一轮业务修改污染
                hook.ResetRuntimeState();
            }

            if (hook != null)
            {
                hook.Creator = creator;
            }

            return hook;
        }

        /// <summary>
        /// 预热对象池，防止战斗中首次实例化卡顿
        /// </summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            PrefabPoolNode node = GetOrCreatePoolNode(prefab);

            for (int i = 0; i < count; i++)
            {
                GameObject instance = Instantiate(prefab, node.Root);
                instance.SetActive(false);

                if (!instance.TryGetComponent<CwcSceneEntityHook>(out var hook))
                {
                    hook = instance.AddComponent<CwcSceneEntityHook>();
                }
                hook.Initialize(prefab);

                node.InactiveStack.Push(hook);
            }
        }

        #endregion

        #region 公开空间感知与区域查询接口 (Public Query Methods)

        /// <summary>
        /// 查询指定中心点与半径范围内的所有活跃实体（零 GC 填充至输出列表）
        /// </summary>
        public void GetEntitiesInRadius(Vector3 center, float radius, List<CwcSceneEntityHook> results)
        {
            GetEntitiesInRadius(center, radius, float.MaxValue, results);
        }

        /// <summary>
        /// 查询指定中心点、水平半径与最大垂直高度差范围内的所有活跃实体（零 GC 填充至输出列表）
        /// </summary>
        public void GetEntitiesInRadius(Vector3 center, float radius, float maxHeightDiff, List<CwcSceneEntityHook> results)
        {
            if (_spatialGrid != null)
            {
                _spatialGrid.QueryRadius(center, radius, maxHeightDiff, results);
            }
        }

        /// <summary>
        /// 统计指定中心点与半径范围内的活跃实体总威胁消耗点数
        /// </summary>
        public int GetTotalCostInRadius(Vector3 center, float radius)
        {
            return GetTotalCostInRadius(center, radius, float.MaxValue);
        }

        /// <summary>
        /// 统计指定中心点、水平半径与最大垂直高度差范围内的活跃实体总威胁消耗点数
        /// </summary>
        public int GetTotalCostInRadius(Vector3 center, float radius, float maxHeightDiff)
        {
            if (_spatialGrid != null)
            {
                return _spatialGrid.GetTotalCostInRadius(center, radius, maxHeightDiff);
            }
            return 0;
        }

        /// <summary>
        /// 添加关注目标（如玩家），用于超距淘汰距离判定
        /// </summary>
        public void AddFocusTarget(Transform target)
        {
            if (target != null && !_focusTargets.Contains(target))
            {
                _focusTargets.Add(target);
                _hasWarnedMissingFocusTarget = false;
            }
        }

        /// <summary>
        /// 移除关注目标
        /// </summary>
        public void RemoveFocusTarget(Transform target)
        {
            if (target != null)
            {
                _focusTargets.Remove(target);
            }
        }

        #endregion

        #region 内部生命周期管理 (Internal Management by Hook)

        /// <summary>
        /// 由 CwcSceneEntityHook.OnEnable 自动调用的激活登记接口
        /// </summary>
        internal void RegisterActiveEntity(CwcSceneEntityHook hook)
        {
            if (hook == null) return;

            if (!_activeEntities.Contains(hook))
            {
                _activeEntities.Add(hook);
                _totalThreatCost += hook.ThreatCost;

                if (_spatialGrid != null)
                {
                    _spatialGrid.Insert(hook);
                }

                // 若实体归属于特定信用点导演模块，自动按该怪物的真实 ThreatCost 扣除预算（单向自动审计）
                if (hook.Creator is CwcCreditDirectorModule creditDirector)
                {
                    creditDirector.ConsumeBudget(hook.ThreatCost);
                }

                OnEntityRegistered?.Invoke(hook);
            }
        }

        /// <summary>
        /// 由 CwcSceneEntityHook.OnDisable 自动调用的注销并回池接口
        /// </summary>
        internal void UnregisterAndRecycle(CwcSceneEntityHook hook)
        {
            if (hook == null) return;

            if (_activeEntities.Remove(hook))
            {
                _totalThreatCost = Mathf.Max(0, _totalThreatCost - hook.ThreatCost);

                if (_spatialGrid != null)
                {
                    _spatialGrid.Remove(hook);
                }

                OnEntityUnregistered?.Invoke(hook);
            }

            // 归入对应的预制体专属分类对象池
            GameObject prefabSource = hook.PrefabSource;
            if (prefabSource != null)
            {
                PrefabPoolNode node = GetOrCreatePoolNode(prefabSource);
                if (node != null)
                {
                    hook.transform.SetParent(node.Root, false);
                    node.InactiveStack.Push(hook);
                }
            }
        }

        /// <summary>
        /// 由 CwcSceneEntityHook.OnDestroy 调用的彻底销毁注销接口
        /// </summary>
        internal void UnregisterDestroyedEntity(CwcSceneEntityHook hook)
        {
            if (hook == null) return;

            if (_activeEntities.Remove(hook))
            {
                _totalThreatCost = Mathf.Max(0, _totalThreatCost - hook.ThreatCost);

                if (_spatialGrid != null)
                {
                    _spatialGrid.Remove(hook);
                }

                OnEntityUnregistered?.Invoke(hook);
            }
        }

        #endregion

        #region 私有辅助方法 (Private Methods)

        private void InitPoolContainer()
        {
            if (_poolContainer == null)
            {
                GameObject holder = new GameObject(PoolContainerName);
                holder.transform.SetParent(this.transform);
                _poolContainer = holder.transform;
            }
        }

        private PrefabPoolNode GetOrCreatePoolNode(GameObject prefab)
        {
            if (prefab == null) return null;

            int prefabId = prefab.GetInstanceID();
            if (!_pools.TryGetValue(prefabId, out var node))
            {
                InitPoolContainer();
                GameObject groupGo = new GameObject("Pool_" + prefab.name);
                groupGo.transform.SetParent(_poolContainer, false);

                node = new PrefabPoolNode
                {
                    Prefab = prefab,
                    Root = groupGo.transform,
                    InactiveStack = new Stack<CwcSceneEntityHook>(16)
                };
                _pools.Add(prefabId, node);
            }
            return node;
        }

        private void UpdateSpatialPositions()
        {
            if (_spatialGrid == null) return;

            int count = _activeEntities.Count;
            for (int i = 0; i < count; i++)
            {
                CwcSceneEntityHook hook = _activeEntities[i];
                if (hook != null)
                {
                    _spatialGrid.UpdatePosition(hook);
                }
            }
        }

        private void CheckCulling()
        {
            // 倒序清理可能已被 Destroy 销毁的空引用
            for (int t = _focusTargets.Count - 1; t >= 0; t--)
            {
                if (_focusTargets[t] == null)
                {
                    _focusTargets.RemoveAt(t);
                }
            }

            int targetCount = _focusTargets.Count;
            if (targetCount == 0)
            {
                // 尝试自动查找 Player 标签对象兜底
                try
                {
                    GameObject playerGo = GameObject.FindWithTag("Player");
                    if (playerGo != null)
                    {
                        _focusTargets.Add(playerGo.transform);
                        targetCount = 1;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning(LogPrefix + "尝试自动查找 Player 标签对象异常：" + ex.Message);
                }

                if (targetCount == 0)
                {
                    if (!_hasWarnedMissingFocusTarget)
                    {
                        _hasWarnedMissingFocusTarget = true;
                        Debug.LogWarning(LogPrefix + "超距淘汰已启用，但未配置任何有效的关注目标（Focus Targets），且场景中未找到带 Player 标签的对象。已跳过本次淘汰检测。");
                    }
                    return;
                }
            }

            _hasWarnedMissingFocusTarget = false;
            float currentTime = Time.time;
            float cullDistSq = _cullDistance * _cullDistance;

            // 倒序遍历，防止淘汰失活时触发注销导致索引错位
            for (int i = _activeEntities.Count - 1; i >= 0; i--)
            {
                if (i >= _activeEntities.Count) continue;

                CwcSceneEntityHook hook = _activeEntities[i];
                if (hook == null) continue;

                // 若配置为不允许超距回收（如 Boss、独特精英），永久豁免
                if (!hook.AllowCulling)
                {
                    continue;
                }

                // 处于出生保护期内的单位不予淘汰
                if (currentTime - hook.SpawnTimestamp < _cullGracePeriod)
                {
                    continue;
                }

                Vector3 entityPos = hook.transform.position;
                bool isAnyTargetInRange = false;

                for (int t = 0; t < targetCount; t++)
                {
                    Transform target = _focusTargets[t];
                    if (target == null) continue;

                    Vector3 diff = target.position - entityPos;
                    // 真实 3D 空间距离判定
                    float distSq = diff.sqrMagnitude;

                    if (distSq <= cullDistSq)
                    {
                        isAnyTargetInRange = true;
                        break;
                    }
                }

                // 超出所有关注目标的淘汰距离范围
                if (!isAnyTargetInRange)
                {
                    // 直接失活，被动触发 OnDisable 自动归池链路
                    hook.Despawn();
                }
            }
        }

        #endregion
    }
}
