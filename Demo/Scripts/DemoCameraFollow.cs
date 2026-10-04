using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 演示场景相机平滑跟随组件 (MonoBehaviour)
    /// 让主摄像机以俯视视角平滑跟随玩家，便于在地牢不同房间与走廊间移动和观察生成效果
    /// 独立封装在 Demo 目录内，零外部业务依赖
    /// </summary>
    [AddComponentMenu("Cwcbb/Demo/Demo Camera Follow")]
    public class DemoCameraFollow : MonoBehaviour
    {
        #region 常量与静态 (Constants & Static)

        private static readonly Quaternion FixedRotation = Quaternion.Euler(52f, 0f, 0f);

        #endregion

        #region Inspector 序列化字段

        [Header("跟随目标 (Target)")]
        [Tooltip("跟随的主角 Transform")]
        [SerializeField] private Transform _target;

        [Header("跟随参数 (Follow Settings)")]
        [Tooltip("相对于主角的固定相机偏移量")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 20f, -14f);

        [Tooltip("平滑插值移动速度")]
        [SerializeField] private float _smoothSpeed = 6f;

        #endregion

        #region 私有非序列化字段

        private bool _hasTarget = false;

        #endregion

        #region 公开属性

        public Transform Target
        {
            get => _target;
            set
            {
                _target = value;
                _hasTarget = _target != null;
            }
        }

        #endregion

        #region Unity 生命周期

        private void Start()
        {
            _hasTarget = _target != null;
            transform.rotation = FixedRotation;
        }

        private void LateUpdate()
        {
            if (!_hasTarget) return;

            Vector3 desiredPosition = _target.position + _offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed * Time.deltaTime);
            transform.rotation = FixedRotation;
        }

        #endregion

        #region 公开操作方法

        /// <summary>
        /// 设定跟随目标，可选择是否立即吸附定位
        /// </summary>
        public void SetTarget(Transform newTarget, bool snapImmediately = false)
        {
            _target = newTarget;
            _hasTarget = _target != null;

            if (_hasTarget && snapImmediately)
            {
                transform.position = _target.position + _offset;
                transform.rotation = FixedRotation;
            }
        }

        #endregion
    }
}
