using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 场景总导演核心调度器
    /// 纯调度中枢，不包含具体业务逻辑；生命周期与场景绑定，支持按需懒创建
    /// </summary>
    [AddComponentMenu("Cwcbb/Scene Director/Cwc Scene Director")]
    public class CwcSceneDirector : MonoBehaviour
    {
        #region 常量与静态成员 (Constants & Static)

        private const string LogPrefix = "[CwcSceneDirector] ";
        private const string DefaultGameObjectName = "[CwcSceneDirector]";

        private static CwcSceneDirector _instance;
        private static readonly Comparison<CwcSceneDirectorModule> ModulePriorityComparison = (a, b) =>
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.ExecutionOrder.CompareTo(b.ExecutionOrder);
        };

        #endregion

        #region Inspector 序列化字段 (Serialized Fields)

        [Header("运行配置 (Runtime Settings)")]
        [Tooltip("是否全局暂停所有模块更新")]
        [SerializeField] private bool _isPaused = false;

        [Tooltip("是否在控制台输出模块生命周期日志")]
        [SerializeField] private bool _enableLifecycleLog = false;

        #endregion

        #region 私有非序列化字段 (Private Fields)

        private readonly List<CwcSceneDirectorModule> _modules = new List<CwcSceneDirectorModule>(32);
        private readonly List<CwcSceneDirectorModule> _pendingAdd = new List<CwcSceneDirectorModule>(8);
        private readonly List<CwcSceneDirectorModule> _pendingRemove = new List<CwcSceneDirectorModule>(8);

        private bool _isUpdating = false;
        private bool _needsSort = false;

        #endregion

        #region 公开属性 (Public Properties)

        /// <summary>
        /// 场景级单例访问点，场景中无实例时将自动懒创建
        /// </summary>
        public static CwcSceneDirector Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _instance = FindFirstObjectByType<CwcSceneDirector>();
#else
                    _instance = FindObjectOfType<CwcSceneDirector>();
#endif
                    if (_instance == null)
                    {
                        GameObject directorGo = new GameObject(DefaultGameObjectName);
                        _instance = directorGo.AddComponent<CwcSceneDirector>();
                        Debug.Log(LogPrefix + "检测到当前场景未配置导演节点，已自动创建 [" + DefaultGameObjectName + "] 实例。");
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 当前场景中是否存在导演实例
        /// </summary>
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// 全局暂停状态
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    if (_isPaused)
                    {
                        PauseAll();
                    }
                    else
                    {
                        ResumeAll();
                    }
                }
            }
        }

        /// <summary>
        /// 当前激活运行中的模块数量
        /// </summary>
        public int ModuleCount => _modules.Count;

        /// <summary>
        /// 模块只读列表（供调试或 Inspector 查看）
        /// </summary>
        public IReadOnlyList<CwcSceneDirectorModule> ActiveModules => _modules;

        /// <summary>
        /// 场景实体对象池与空间感知管理器便捷访问点（平行独立的场景服务）
        /// </summary>
        public CwcSceneEntityManager EntityManager => CwcSceneEntityManager.Instance;

        #endregion

        #region Unity 生命周期 (Unity Lifecycle)

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(LogPrefix + "检测到场景中存在重复的 CwcSceneDirector 实例，正在移除冗余组件：" + gameObject.name);
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void Update()
        {
            ProcessPendingChanges();

            if (_isPaused) return;

            _isUpdating = true;
            float deltaTime = Time.deltaTime;
            int count = _modules.Count;

            for (int i = 0; i < count; i++)
            {
                CwcSceneDirectorModule module = _modules[i];
                if (module == null) continue;

                if (module.IsCompleted)
                {
                    if (!_pendingRemove.Contains(module))
                    {
                        _pendingRemove.Add(module);
                    }
                    continue;
                }

                if (module.IsRunning)
                {
                    module.OnTick(deltaTime);

                    if (module.IsCompleted && !_pendingRemove.Contains(module))
                    {
                        _pendingRemove.Add(module);
                    }
                }
            }

            _isUpdating = false;

            ProcessPendingRemovals();
        }

        private void FixedUpdate()
        {
            if (_isPaused) return;

            float fixedDeltaTime = Time.fixedDeltaTime;
            int count = _modules.Count;

            for (int i = 0; i < count; i++)
            {
                CwcSceneDirectorModule module = _modules[i];
                if (module != null && module.IsRunning)
                {
                    module.OnFixedTick(fixedDeltaTime);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            int count = _modules.Count;
            for (int i = 0; i < count; i++)
            {
                CwcSceneDirectorModule module = _modules[i];
                if (module != null && module.IsEnabled)
                {
                    module.OnDrawGizmosSelected();
                }
            }
        }

        private void OnDestroy()
        {
            int moduleCount = _modules.Count;
            for (int i = 0; i < moduleCount; i++)
            {
                CwcSceneDirectorModule module = _modules[i];
                if (module != null)
                {
                    module.OnStop();
                    module.OnDispose();
                }
            }
            _modules.Clear();

            int pendingAddCount = _pendingAdd.Count;
            for (int i = 0; i < pendingAddCount; i++)
            {
                CwcSceneDirectorModule module = _pendingAdd[i];
                if (module != null)
                {
                    module.OnDispose();
                }
            }
            _pendingAdd.Clear();
            _pendingRemove.Clear();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion

        #region 公开静态便捷方法 (Public Static Methods)

        /// <summary>
        /// 注册并激活导演模块（若场景无导演将自动懒创建）
        /// </summary>
        public static T Register<T>(T module) where T : CwcSceneDirectorModule
        {
            return Instance.RegisterModule(module);
        }

        /// <summary>
        /// 注销指定导演模块
        /// </summary>
        public static bool Unregister(CwcSceneDirectorModule module)
        {
            if (HasInstance)
            {
                return Instance.UnregisterModule(module);
            }
            return false;
        }

        #endregion

        #region 公开实例管理方法 (Public Methods)

        /// <summary>
        /// 注册导演模块
        /// </summary>
        public T RegisterModule<T>(T module) where T : CwcSceneDirectorModule
        {
            if (module == null)
            {
                Debug.LogError(LogPrefix + "注册模块失败：传入的模块实例为 null！");
                return null;
            }

            if (_pendingRemove.Contains(module))
            {
                _pendingRemove.Remove(module);
            }

            if (_modules.Contains(module) || _pendingAdd.Contains(module))
            {
                Debug.LogWarning(LogPrefix + "模块已存在或已在等待注册队列中，忽略重复注册：" + module.GetType().Name);
                return module;
            }

            if (_isUpdating)
            {
                _pendingAdd.Add(module);
            }
            else
            {
                _modules.Add(module);
                module.OnInit(this);
                module.OnStart();
                _needsSort = true;

                if (_enableLifecycleLog)
                {
                    Debug.Log(LogPrefix + "成功注册并启动模块：" + module.GetType().Name);
                }
            }

            return module;
        }

        /// <summary>
        /// 注销并停止导演模块
        /// </summary>
        public bool UnregisterModule(CwcSceneDirectorModule module)
        {
            if (module == null)
            {
                Debug.LogError(LogPrefix + "注销模块失败：传入的模块实例为 null！");
                return false;
            }

            if (_pendingAdd.Contains(module))
            {
                _pendingAdd.Remove(module);
                module.OnDispose();
                return true;
            }

            if (!_modules.Contains(module))
            {
                return false;
            }

            if (_isUpdating)
            {
                if (!_pendingRemove.Contains(module))
                {
                    _pendingRemove.Add(module);
                }
            }
            else
            {
                _modules.Remove(module);
                module.OnStop();
                module.OnDispose();

                if (_enableLifecycleLog)
                {
                    Debug.Log(LogPrefix + "已注销并释放模块：" + module.GetType().Name);
                }
            }

            return true;
        }

        /// <summary>
        /// 获取指定类型的首个模块实例
        /// </summary>
        public T GetModule<T>() where T : CwcSceneDirectorModule
        {
            int count = _modules.Count;
            for (int i = 0; i < count; i++)
            {
                if (_modules[i] is T matched)
                {
                    return matched;
                }
            }
            return null;
        }

        /// <summary>
        /// 尝试获取指定类型的首个模块实例
        /// </summary>
        public bool TryGetModule<T>(out T module) where T : CwcSceneDirectorModule
        {
            module = GetModule<T>();
            return module != null;
        }

        /// <summary>
        /// 获取所有指定类型的模块（零 GC 填充到输出列表）
        /// </summary>
        public void GetModules<T>(List<T> results) where T : CwcSceneDirectorModule
        {
            if (results == null)
            {
                Debug.LogError(LogPrefix + "GetModules 失败：传入的输出结果列表为 null！");
                return;
            }

            results.Clear();
            int count = _modules.Count;
            for (int i = 0; i < count; i++)
            {
                if (_modules[i] is T matched)
                {
                    results.Add(matched);
                }
            }
        }

        /// <summary>
        /// 暂停所有已激活模块
        /// </summary>
        public void PauseAll()
        {
            int count = _modules.Count;
            for (int i = 0; i < count; i++)
            {
                CwcSceneDirectorModule module = _modules[i];
                if (module != null && !module.IsPaused)
                {
                    module.SetPaused(true);
                }
            }
        }

        /// <summary>
        /// 恢复所有已暂停模块
        /// </summary>
        public void ResumeAll()
        {
            int count = _modules.Count;
            for (int i = 0; i < count; i++)
            {
                CwcSceneDirectorModule module = _modules[i];
                if (module != null && module.IsPaused)
                {
                    module.SetPaused(false);
                }
            }
        }

        /// <summary>
        /// 标记模块列表需要重新按优先级排序
        /// </summary>
        public void MarkSortNeeded()
        {
            _needsSort = true;
        }

        #endregion

        #region 私有辅助方法 (Private Methods)

        private void ProcessPendingChanges()
        {
            ProcessPendingRemovals();

            int addCount = _pendingAdd.Count;
            if (addCount > 0)
            {
                for (int i = 0; i < addCount; i++)
                {
                    CwcSceneDirectorModule module = _pendingAdd[i];
                    if (module == null) continue;

                    if (!_modules.Contains(module))
                    {
                        _modules.Add(module);
                        module.OnInit(this);
                        module.OnStart();
                        _needsSort = true;

                        if (_enableLifecycleLog)
                        {
                            Debug.Log(LogPrefix + "处理待添加队列，启动模块：" + module.GetType().Name);
                        }
                    }
                }
                _pendingAdd.Clear();
            }

            if (_needsSort)
            {
                _modules.Sort(ModulePriorityComparison);
                _needsSort = false;
            }
        }

        private void ProcessPendingRemovals()
        {
            int removeCount = _pendingRemove.Count;
            if (removeCount == 0) return;

            for (int i = 0; i < removeCount; i++)
            {
                CwcSceneDirectorModule module = _pendingRemove[i];
                if (module == null) continue;

                if (_modules.Remove(module))
                {
                    module.OnStop();
                    module.OnDispose();

                    if (_enableLifecycleLog)
                    {
                        Debug.Log(LogPrefix + "处理待移除队列，已释放模块：" + module.GetType().Name);
                    }
                }
            }
            _pendingRemove.Clear();
        }

        #endregion
    }
}
