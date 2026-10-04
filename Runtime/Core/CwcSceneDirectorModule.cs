using System.Collections;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 场景导演模块抽象基类
    /// 纯 C# 类逻辑载体，由 CwcSceneDirector 统一驱动生命周期
    /// </summary>
    public abstract class CwcSceneDirectorModule
    {
        #region 静态与常量 (Constants & Static)

        private const string LogPrefix = "[CwcSceneDirectorModule] ";

        #endregion

        #region 私有字段 (Private Fields)

        private CwcSceneDirector _director;
        private int _executionOrder = 0;
        private bool _isEnabled = true;
        private bool _isPaused = false;
        private bool _isCompleted = false;
        private bool _isStarted = false;

        #endregion

        #region 公开属性 (Public Properties)

        /// <summary>
        /// 所属的场景导演调度宿主
        /// </summary>
        public CwcSceneDirector Director => _director;

        /// <summary>
        /// 执行优先级（数值越小越优先执行）
        /// </summary>
        public int ExecutionOrder
        {
            get => _executionOrder;
            set
            {
                if (_executionOrder != value)
                {
                    _executionOrder = value;
                    if (_director != null)
                    {
                        _director.MarkSortNeeded();
                    }
                }
            }
        }

        /// <summary>
        /// 是否处于启用状态
        /// </summary>
        public bool IsEnabled => _isEnabled;

        /// <summary>
        /// 是否处于暂停状态
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// 是否已完成逻辑生命周期（标记为完成后由宿主在当帧结束时自动回收）
        /// </summary>
        public bool IsCompleted => _isCompleted;

        /// <summary>
        /// 是否已经触发过启动
        /// </summary>
        public bool IsStarted => _isStarted;

        /// <summary>
        /// 模块当前是否处于有效运行状态
        /// </summary>
        public bool IsRunning => _isEnabled && !_isPaused && !_isCompleted;

        #endregion

        #region 模块生命周期 (Lifecycle)

        /// <summary>
        /// 初始化模块，注入宿主上下文
        /// </summary>
        public virtual void OnInit(CwcSceneDirector director)
        {
            _director = director;
        }

        /// <summary>
        /// 模块开始执行
        /// </summary>
        public virtual void OnStart()
        {
            _isStarted = true;
        }

        /// <summary>
        /// 逻辑帧更新（仅当 IsRunning 为 true 时调用）
        /// </summary>
        public virtual void OnTick(float deltaTime)
        {
        }

        /// <summary>
        /// 物理帧更新（仅当 IsRunning 为 true 时调用）
        /// </summary>
        public virtual void OnFixedTick(float fixedDeltaTime)
        {
        }

        /// <summary>
        /// 模块暂停响应
        /// </summary>
        public virtual void OnPause()
        {
        }

        /// <summary>
        /// 模块恢复响应
        /// </summary>
        public virtual void OnResume()
        {
        }

        /// <summary>
        /// 模块停止响应
        /// </summary>
        public virtual void OnStop()
        {
        }

        /// <summary>
        /// 模块彻底释放与清理
        /// </summary>
        public virtual void OnDispose()
        {
            _director = null;
            _isStarted = false;
        }

        /// <summary>
        /// 调试绘制回调（由宿主 OnDrawGizmosSelected 广播）
        /// </summary>
        public virtual void OnDrawGizmosSelected()
        {
            OnDrawGizmos();
        }

        /// <summary>
        /// 调试绘制回调（已弃用，建议统一重写 OnDrawGizmosSelected 保证仅选中显示）
        /// </summary>
        [System.Obsolete("建议统一重写 OnDrawGizmosSelected")]
        public virtual void OnDrawGizmos()
        {
        }

        #endregion

        #region 公开控制与辅助方法 (Public Methods)

        /// <summary>
        /// 设置模块启用或禁用
        /// </summary>
        public void SetActive(bool active)
        {
            if (_isEnabled == active) return;

            _isEnabled = active;
            if (_isEnabled)
            {
                if (!_isStarted)
                {
                    OnStart();
                }
            }
            else
            {
                OnStop();
            }
        }

        /// <summary>
        /// 设置模块暂停或继续
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (_isPaused == paused) return;

            _isPaused = paused;
            if (_isPaused)
            {
                OnPause();
            }
            else
            {
                OnResume();
            }
        }

        /// <summary>
        /// 标记模块已完成其业务逻辑
        /// 场景导演将在本帧 Tick 结束后自动安全卸载并释放此模块
        /// </summary>
        public void Complete()
        {
            if (_isCompleted) return;

            _isCompleted = true;
        }

        /// <summary>
        /// 在宿主 MonoBehaviour 上启动协程
        /// </summary>
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            if (_director == null)
            {
                Debug.LogError(LogPrefix + "无法启动协程：宿主 Director 为空或模块尚未注册！");
                return null;
            }

            if (routine == null)
            {
                Debug.LogError(LogPrefix + "无法启动协程：传入的 IEnumerator 为空！");
                return null;
            }

            return _director.StartCoroutine(routine);
        }

        /// <summary>
        /// 停止宿主上的指定协程
        /// </summary>
        public void StopCoroutine(Coroutine routine)
        {
            if (_director != null && routine != null)
            {
                _director.StopCoroutine(routine);
            }
        }

        /// <summary>
        /// 停止宿主上的指定协程迭代器
        /// </summary>
        public void StopCoroutine(IEnumerator routine)
        {
            if (_director != null && routine != null)
            {
                _director.StopCoroutine(routine);
            }
        }

        #endregion
    }
}
