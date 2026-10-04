using System.Collections.Generic;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 演示场景简易玩家移动控制器 (MonoBehaviour)
    /// 仅供 Demo 测试场景中通过键盘 WASD 移动胶囊体以触发关卡刷怪区域
    /// 独立封装在 Demo 目录内，随时可安全删除
    /// </summary>
    [AddComponentMenu("Cwcbb/Demo/Demo Player Controller")]
    public class DemoPlayerController : MonoBehaviour
    {
        #region Inspector 序列化字段

        [Header("移动参数 (Movement Settings)")]
        [Tooltip("水平移动速度")]
        [SerializeField] private float _moveSpeed = 8f;

        [Header("模拟战斗消灭 (Simulated Combat)")]
        [Tooltip("是否启用接近自动消灭怪物功能")]
        [SerializeField] private bool _enableAutoEliminate = true;

        [Tooltip("消灭敌人的判定半径（米）")]
        [SerializeField] private float _eliminateRadius = 2.2f;

        #endregion

        #region 私有非序列化字段

        private readonly List<CwcSceneEntityHook> _nearbyBuffer = new List<CwcSceneEntityHook>(16);
        private int _totalKills = 0;

        #endregion

        #region 公开属性

        public int TotalKills => _totalKills;

        #endregion

        #region Unity 生命周期

        private void Update()
        {
            // 1. WASD 角色平移移动（纯 Transform 平移，零物理碰撞矩阵依赖）
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 direction = new Vector3(h, 0f, v).normalized;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.forward = direction;
                transform.position += direction * (_moveSpeed * Time.deltaTime);
            }

            // 2. 地面高度自适应（纯空间射线探测，不走物理引擎碰撞矩阵模拟）
            if (Physics.Raycast(transform.position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f))
            {
                if (hit.normal.y > 0.5f)
                {
                    Vector3 pos = transform.position;
                    pos.y = hit.point.y + 1f; // 胶囊体中心高度
                    transform.position = pos;
                }
            }
            else
            {
                // 绝对防跌落兜底：若未探测到任何地面，锁在基准高度，绝不向下跌落
                if (transform.position.y < 0.5f)
                {
                    Vector3 pos = transform.position;
                    pos.y = 1f;
                    transform.position = pos;
                }
            }

            // 3. 模拟战斗：自动消灭/回收贴近主角的实体
            if (_enableAutoEliminate)
            {
                CheckAndEliminateNearbyEntities();
            }
        }

        private void OnDrawGizmosSelected()
        {
            // 绘制近战消灭范围
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _eliminateRadius);
        }

        #endregion

        #region 公开操作方法

        /// <summary>
        /// 安全瞬间传送玩家至目标世界坐标
        /// </summary>
        public void TeleportTo(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }

        #endregion

        #region 私有辅助方法

        private void CheckAndEliminateNearbyEntities()
        {
            var manager = CwcSceneEntityManager.Instance;
            if (manager == null) return;

            // 借助管理器空间网格高效感知周边实体
            manager.GetEntitiesInRadius(transform.position, _eliminateRadius, _nearbyBuffer);

            int count = _nearbyBuffer.Count;
            for (int i = 0; i < count; i++)
            {
                var hook = _nearbyBuffer[i];
                if (hook != null && hook.gameObject.activeSelf)
                {
                    _totalKills++;
                    Debug.Log($"[DemoPlayerController] 模拟战斗：消灭并回收单位 [{hook.gameObject.name}]，释放威胁度：{hook.ThreatCost} 点！总击杀：{_totalKills}");

                    // 直接失活实体，触发 Hook.OnDisable 自动向单位管理器注销并回池！
                    hook.Despawn();
                }
            }
        }

        #endregion
    }
}
