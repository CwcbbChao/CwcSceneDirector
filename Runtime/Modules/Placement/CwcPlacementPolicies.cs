using System;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 放置模式枚举（保留作轻量标记或向后兼容）
    /// </summary>
    public enum PlacementMode
    {
        Free = 0,
        WallSnapped = 1,
        OpenCenter = 2
    }

    /// <summary>
    /// 空间放置与物理校验策略契约接口
    /// 彻底消除核心调度器中的 enum 与 switch-case 分支，逻辑完全由具体策略多态处理
    /// </summary>
    public interface IPlacementStrategy
    {
        /// <summary>
        /// 校验并对齐候选落点，生成最终合法位姿
        /// </summary>
        /// <param name="candidatePoint">采样候选点</param>
        /// <param name="clusterCenter">群落中心</param>
        /// <param name="footprintRadius">物体自身的物理底盘占用半径 R（完全从原生 Collider 自动计算）</param>
        /// <param name="context">空间上下文</param>
        /// <param name="finalPose">输出计算后的最终位姿</param>
        /// <returns>若该点物理合法且安全则返回 true</returns>
        bool TryProcessPose(
            Vector3 candidatePoint,
            Vector3 clusterCenter,
            float footprintRadius,
            CwcPlacementContext context,
            out Pose finalPose);
    }

    #region 内置物理策略实现 (Built-in Placement Strategies)

    /// <summary>
    /// 自由地面放置策略（默认：平稳贴地 + 原生物理底盘悬崖防空 + 原生物理穿模与 2R 互斥）
    /// </summary>
    public class FreeGroundPlacementStrategy : IPlacementStrategy
    {
        public static readonly FreeGroundPlacementStrategy Instance = new FreeGroundPlacementStrategy();

        public virtual bool TryProcessPose(
            Vector3 candidatePoint,
            Vector3 clusterCenter,
            float footprintRadius,
            CwcPlacementContext context,
            out Pose finalPose)
        {
            finalPose = Pose.identity;
            var settings = context.Settings;
            float r = Mathf.Max(0.1f, footprintRadius);

            // 1. 地面贴合
            if (!CwcPlacementSpatialUtil.TrySnapToGround(candidatePoint, settings, out Vector3 groundPos))
            {
                return false;
            }

            // 2. 原生物理底盘悬崖防空探测（以自身 R 向四周探测，防止半个身子悬空在深渊虚空或断崖边）
            if (!CwcPlacementSpatialUtil.IsLedgeSafe(groundPos, r, settings))
            {
                return false;
            }

            // 3. 原生物理障碍穿模检查（以物理半径 R 检测球）
            if (!CwcPlacementSpatialUtil.IsObstacleFree(groundPos, r, settings))
            {
                return false;
            }

            // 4. 单位互斥排斥检查（以自身 2R 物理直径排斥）
            float separation = r * 2f;
            if (!CwcPlacementSpatialUtil.IsSeparationFree(groundPos, separation, context.EntityManager))
            {
                return false;
            }

            // 5. 随机自然水平朝向
            Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            finalPose = new Pose(groundPos, rot);
            return true;
        }
    }

    /// <summary>
    /// 贴墙放置策略（水平探测墙壁 + 依物理半径 R 自动推开严丝合缝相切 + 背墙朝向 + 悬崖防空）
    /// </summary>
    public class WallSnappedPlacementStrategy : IPlacementStrategy
    {
        public static readonly WallSnappedPlacementStrategy Instance = new WallSnappedPlacementStrategy();

        public virtual bool TryProcessPose(
            Vector3 candidatePoint,
            Vector3 clusterCenter,
            float footprintRadius,
            CwcPlacementContext context,
            out Pose finalPose)
        {
            finalPose = Pose.identity;
            var settings = context.Settings;
            float r = Mathf.Max(0.1f, footprintRadius);

            // 1. 地面贴合
            if (!CwcPlacementSpatialUtil.TrySnapToGround(candidatePoint, settings, out Vector3 groundPos))
            {
                return false;
            }

            // 2. 靠墙探测：自动根据物体自身半径 R 进行相切吸附与法线对齐
            Vector3 targetPos = groundPos;
            Vector3 wallNormal = Vector3.forward;
            bool snapped = CwcPlacementSpatialUtil.TrySnapToWall(groundPos, r, settings, out targetPos, out wallNormal);

            // 3. 悬崖与深渊底盘安全探测
            if (!CwcPlacementSpatialUtil.IsLedgeSafe(targetPos, r, settings))
            {
                return false;
            }

            // 4. 障碍穿模检查
            if (!CwcPlacementSpatialUtil.IsObstacleFree(targetPos, r, settings))
            {
                return false;
            }

            // 5. 单位互斥排斥检查
            float separation = r * 2f;
            if (!CwcPlacementSpatialUtil.IsSeparationFree(targetPos, separation, context.EntityManager))
            {
                return false;
            }

            // 6. 朝向计算：若成功吸附则背对墙面面向开阔室内，否则随机朝向
            Quaternion rot = snapped && wallNormal.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(new Vector3(wallNormal.x, 0f, wallNormal.z))
                : Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            finalPose = new Pose(targetPos, rot);
            return true;
        }
    }

    /// <summary>
    /// 开阔地放置策略（大范围无遮挡探测 + 贴地与悬崖防空）
    /// </summary>
    public class OpenSpacePlacementStrategy : IPlacementStrategy
    {
        public static readonly OpenSpacePlacementStrategy Instance = new OpenSpacePlacementStrategy();

        public virtual bool TryProcessPose(
            Vector3 candidatePoint,
            Vector3 clusterCenter,
            float footprintRadius,
            CwcPlacementContext context,
            out Pose finalPose)
        {
            finalPose = Pose.identity;
            var settings = context.Settings;
            float r = Mathf.Max(0.1f, footprintRadius);

            // 1. 地面贴合
            if (!CwcPlacementSpatialUtil.TrySnapToGround(candidatePoint, settings, out Vector3 groundPos))
            {
                return false;
            }

            // 2. 开阔腹地判定：周围不能有近距离墙体
            if (!CwcPlacementSpatialUtil.IsOpenSpace(groundPos, settings))
            {
                return false;
            }

            // 3. 悬崖底盘防空探测
            if (!CwcPlacementSpatialUtil.IsLedgeSafe(groundPos, r, settings))
            {
                return false;
            }

            // 4. 障碍穿模检查
            if (!CwcPlacementSpatialUtil.IsObstacleFree(groundPos, r, settings))
            {
                return false;
            }

            // 5. 单位互斥
            if (!CwcPlacementSpatialUtil.IsSeparationFree(groundPos, r * 2f, context.EntityManager))
            {
                return false;
            }

            Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            finalPose = new Pose(groundPos, rot);
            return true;
        }
    }

    #endregion

    /// <summary>
    /// 常用无状态摆放策略静态门面
    /// </summary>
    public static class CwcPlacementStrategies
    {
        public static readonly IPlacementStrategy Free = FreeGroundPlacementStrategy.Instance;
        public static readonly IPlacementStrategy WallSnapped = WallSnappedPlacementStrategy.Instance;
        public static readonly IPlacementStrategy OpenCenter = OpenSpacePlacementStrategy.Instance;

        /// <summary>
        /// 根据老旧 PlacementMode 枚举转换为对应策略实例
        /// </summary>
        public static IPlacementStrategy FromMode(PlacementMode mode)
        {
            switch (mode)
            {
                case PlacementMode.WallSnapped: return WallSnapped;
                case PlacementMode.OpenCenter: return OpenCenter;
                default: return Free;
            }
        }
    }
}
