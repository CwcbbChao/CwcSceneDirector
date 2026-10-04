using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 空间选点与物理环境校验算法工具库
    /// 提供宏观泊松盘均匀采样、地面贴合、靠墙探测、开阔地判定与朝向计算
    /// </summary>
    public static class CwcPlacementSpatialUtil
    {
        #region 常量与静态 (Constants & Static)

        private const int PoissonK = 30; // 泊松盘单个点的最大候选采样次数

        private static int[] s_GridBuffer = new int[1024];
        private static readonly List<Vector2> s_SamplePoints = new List<Vector2>(128);
        private static readonly List<int> s_ActiveList = new List<int>(128);

        #endregion

        #region 宏观泊松盘采样 (Macro Poisson Disk Sampling)

        /// <summary>
        /// 在以 center 为中心、radius 为半径的圆形区域内，进行均匀离散的泊松盘采样（零 GC 复用缓冲区）
        /// 保证所有生成点彼此之间的间距不小于 minDistance
        /// </summary>
        public static void SamplePoissonPoints(
            Vector3 center,
            float radius,
            float minDistance,
            int maxPoints,
            List<Vector3> results)
        {
            if (results == null) return;
            results.Clear();

            if (radius <= 0f || minDistance <= 0f || maxPoints <= 0) return;

            float cellSize = minDistance / Mathf.Sqrt(2f);
            int gridDim = Mathf.CeilToInt((radius * 2f) / cellSize);
            int totalCells = gridDim * gridDim;

            if (s_GridBuffer.Length < totalCells)
            {
                int newSize = Mathf.Max(s_GridBuffer.Length * 2, totalCells);
                s_GridBuffer = new int[newSize];
            }

            for (int i = 0; i < totalCells; i++)
            {
                s_GridBuffer[i] = -1;
            }

            s_SamplePoints.Clear();
            s_ActiveList.Clear();

            // 初始种子点在圆盘范围内随机选取（等面积开方采样），避免多次生成时原点固定在正中心产生环形真空排斥区
            float seedAngle = UnityEngine.Random.value * Mathf.PI * 2f;
            float seedDist = Mathf.Sqrt(UnityEngine.Random.value) * (radius * 0.75f);
            Vector2 firstPoint = new Vector2(Mathf.Cos(seedAngle), Mathf.Sin(seedAngle)) * seedDist;
            s_SamplePoints.Add(firstPoint);
            s_ActiveList.Add(0);

            int firstGridX = Mathf.FloorToInt((firstPoint.x + radius) / cellSize);
            int firstGridY = Mathf.FloorToInt((firstPoint.y + radius) / cellSize);
            if (firstGridX >= 0 && firstGridX < gridDim && firstGridY >= 0 && firstGridY < gridDim)
            {
                s_GridBuffer[firstGridX * gridDim + firstGridY] = 0;
            }

            float minDistSq = minDistance * minDistance;
            float radiusSq = radius * radius;

            while (s_ActiveList.Count > 0 && s_SamplePoints.Count < maxPoints)
            {
                int activeIndex = Random.Range(0, s_ActiveList.Count);
                int pointIndex = s_ActiveList[activeIndex];
                Vector2 currentPoint = s_SamplePoints[pointIndex];
                bool found = false;

                for (int attempt = 0; attempt < PoissonK; attempt++)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    float distance = Random.Range(minDistance, 2f * minDistance);
                    Vector2 candidate = currentPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                    if (candidate.sqrMagnitude > radiusSq) continue;

                    int gx = Mathf.FloorToInt((candidate.x + radius) / cellSize);
                    int gy = Mathf.FloorToInt((candidate.y + radius) / cellSize);

                    if (gx < 0 || gx >= gridDim || gy < 0 || gy >= gridDim) continue;

                    // 检查相邻格子间距
                    bool isTooClose = false;
                    int minX = Mathf.Max(0, gx - 2);
                    int maxX = Mathf.Min(gridDim - 1, gx + 2);
                    int minY = Mathf.Max(0, gy - 2);
                    int maxY = Mathf.Min(gridDim - 1, gy + 2);

                    for (int x = minX; x <= maxX && !isTooClose; x++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            int neighborIndex = s_GridBuffer[x * gridDim + y];
                            if (neighborIndex != -1)
                            {
                                if ((s_SamplePoints[neighborIndex] - candidate).sqrMagnitude < minDistSq)
                                {
                                    isTooClose = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!isTooClose)
                    {
                        s_SamplePoints.Add(candidate);
                        int newIndex = s_SamplePoints.Count - 1;
                        s_ActiveList.Add(newIndex);
                        s_GridBuffer[gx * gridDim + gy] = newIndex;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    s_ActiveList.RemoveAt(activeIndex);
                }
            }

            int count = s_SamplePoints.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 pt = s_SamplePoints[i];
                results.Add(new Vector3(center.x + pt.x, center.y, center.z + pt.y));
            }

            s_SamplePoints.Clear();
            s_ActiveList.Clear();
        }

        #endregion

        #region 葵花黄金分割均匀采样 (Sunflower / Vogel Spiral Sampling)

        private const float GoldenAngle = 2.39996323f; // 黄金分割角 137.50776°（弧度制）

        /// <summary>
        /// 葵花黄金角均匀点阵采样（Sunflower / Vogel Spiral）
        /// 在以 center 为中心、spreadRadius 为半径的圆盘内，生成分布极度均匀、不扎堆的候选坐标点
        /// 首个点稳居中心 (r = 0)，后续点按黄金角向外旋绕展开，大怪居中，小怪匀称环绕
        /// </summary>
        public static void SampleSunflowerPoints(
            Vector3 center,
            float spreadRadius,
            int count,
            List<Vector3> results,
            float innerRadius = 0.8f,
            float randomAngleOffset = -1f)
        {
            if (results == null) return;
            results.Clear();

            if (count <= 0) return;

            // 1. 首个点稳居中心 (r = 0)
            results.Add(center);
            if (count == 1) return;

            // 2. 整体随机旋转偏角，保持点阵内部严谨但每次生成朝向不同，兼具自然感
            float baseAngle = randomAngleOffset >= 0f ? randomAngleOffset : UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            float effectiveInner = Mathf.Min(innerRadius, spreadRadius * 0.35f);
            if (effectiveInner < 0f) effectiveInner = 0f;

            // 3. 后续点按黄金螺旋扩散
            for (int i = 1; i < count; i++)
            {
                float angle = baseAngle + i * GoldenAngle;
                // 面积与半径平方成正比，等比开方实现面密度处处相等
                float t = Mathf.Sqrt((float)i / (count - 1));
                float r = Mathf.Lerp(effectiveInner, spreadRadius, t);

                Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
                results.Add(pos);
            }
        }

        #endregion

        #region 环形等面积采样 (Annulus Sampling)

        /// <summary>
        /// 在环形区域（Annulus / Donut，innerRadius ~ outerRadius）内等面积均匀采样单个随机点
        /// 避免怪物贴脸刷新在玩家身边，同时保证环内任意位置面密度处处相等
        /// </summary>
        public static Vector3 SampleAnnulusPoint(Vector3 center, float innerRadius, float outerRadius)
        {
            float safeInner = Mathf.Max(0f, innerRadius);
            float safeOuter = Mathf.Max(safeInner + 0.1f, outerRadius);
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float innerSq = safeInner * safeInner;
            float outerSq = safeOuter * safeOuter;
            float dist = Mathf.Sqrt(UnityEngine.Random.Range(innerSq, outerSq));
            return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
        }

        #endregion

        #region 地面探测与有效性校验 (Ground & Validation)

        /// <summary>
        /// 尝试将候选坐标吸附到合法地面，并进行 NavMesh 二次校验
        /// </summary>
        public static bool TrySnapToGround(
            Vector3 candidatePoint,
            CwcPlacementSettings settings,
            out Vector3 snappedGroundPoint)
        {
            settings = settings ?? CwcSceneDirectorSettings.GlobalPlacementSettings;
            snappedGroundPoint = candidatePoint;

            Vector3 rayOrigin = candidatePoint + Vector3.up * settings.RaycastOriginHeight;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, settings.MaxRaycastDistance, settings.GroundLayer))
            {
                // 确保命中表面为平坦或缓坡地面（法线朝上，杜绝吸附到垂直墙壁或陡峭侧面）
                if (hit.normal.y < 0.5f)
                {
                    return false;
                }

                snappedGroundPoint = hit.point;

                if (settings.UseNavMesh)
                {
                    if (NavMesh.SamplePosition(snappedGroundPoint, out NavMeshHit navHit, settings.NavMeshSampleRange, NavMesh.AllAreas))
                    {
                        snappedGroundPoint = navHit.position;
                        return true;
                    }
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// <summary>
        /// 从预制体原生碰撞体或网格边界中，全自动计算水平物理底盘占用半径 R
        /// 预制体 100% 保持纯净，不需要挂载任何插件专属组件或实现特定接口
        /// </summary>
        public static float GetPhysicalFootprintRadius(GameObject prefab)
        {
            if (prefab == null) return 0.5f;

            // 1. 优先从原生 Collider 包围盒计算水平半宽
            var col = prefab.GetComponentInChildren<Collider>();
            if (col != null)
            {
                var extents = col.bounds.extents;
                float r = Mathf.Max(extents.x, extents.z);
                if (r > 0.05f) return r;
            }

            // 2. 尝试从 CharacterController 提取半径
            var cc = prefab.GetComponentInChildren<CharacterController>();
            if (cc != null && cc.radius > 0.05f)
            {
                return cc.radius;
            }

            // 3. 尝试从 Renderer 网格边界提取
            var rend = prefab.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var extents = rend.bounds.extents;
                float r = Mathf.Max(extents.x, extents.z);
                if (r > 0.05f) return r;
            }

            return 0.5f;
        }

        /// <summary>
        /// 原生物理底盘悬崖与深渊安全检测
        /// 以实体自身物理半径 footprintRadius，向四周发射 4 根垂直下沉射线
        /// 若任意探测点跌入虚空或落差大于台阶阈值，判定为处于悬崖边缘危险浮空
        /// </summary>
        public static bool IsLedgeSafe(
            Vector3 point,
            float footprintRadius,
            CwcPlacementSettings settings)
        {
            settings = settings ?? CwcSceneDirectorSettings.GlobalPlacementSettings;
            float r = Mathf.Max(0.15f, footprintRadius);
            float originHeight = settings.RaycastOriginHeight;
            float maxDist = settings.MaxRaycastDistance;
            LayerMask groundLayer = settings.GroundLayer;
            float maxStepHeight = 1.0f; // 默认允许的台阶最大高度差

            // 水平 4 个方向（前、后、左、右）探测底盘边缘支撑
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
                Vector3 probePoint = point + offset;
                Vector3 rayOrigin = probePoint + Vector3.up * originHeight;

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxDist, groundLayer))
                {
                    // 检测与落点中心的高低落差，防止站在高处断崖边
                    if (Mathf.Abs(hit.point.y - point.y) > maxStepHeight)
                    {
                        return false;
                    }
                }
                else
                {
                    // 未命中地面，脚底完全悬空在深渊虚空之上！
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 检查指定落点是否没有与障碍物或墙壁发生穿模（支持以自身物理半径 R 为基准，未指定则使用配置默认检测半径）
        /// </summary>
        public static bool IsObstacleFree(Vector3 point, float footprintRadius, CwcPlacementSettings settings)
        {
            settings = settings ?? CwcSceneDirectorSettings.GlobalPlacementSettings;
            LayerMask obstacleMask = settings.EffectiveObstacleLayer;
            if (obstacleMask == 0) return true;

            float r = footprintRadius > 0.05f ? Mathf.Max(0.15f, footprintRadius) : settings.ObstacleCheckRadius;
            Vector3 checkCenter = point + Vector3.up * Mathf.Max(0.2f, r);
            return !Physics.CheckSphere(checkCenter, r, obstacleMask);
        }

        /// <summary>
        /// 检查指定落点是否没有与障碍物发生穿模（使用配置默认检测半径）
        /// </summary>
        public static bool IsObstacleFree(Vector3 point, CwcPlacementSettings settings)
        {
            return IsObstacleFree(point, 0f, settings);
        }

        /// <summary>
        /// 检查指定落点与场上已有单位是否满足最小互斥排斥距离
        /// </summary>
        public static bool IsSeparationFree(Vector3 point, float separationRadius, CwcSceneEntityManager entityManager)
        {
            if (separationRadius <= 0.05f || entityManager == null) return true;

            return entityManager.GetTotalCostInRadius(point, separationRadius) == 0;
        }

        #endregion

        #region 靠墙与开阔地策略探测 (Wall & OpenSpace Probing)

        /// <summary>
        /// 靠墙探测：向四周水平发射射线探测最近墙壁，并将落点吸附在安全边距处
        /// 若传入有效 footprintRadius，则根据物体自身的物理底盘占用半径 R 向外推开 (R + 0.05m)，严丝合缝贴合墙壁
        /// 仅探测 WallLayer，杜绝吸附到已放置的宝箱等交互障碍物上产生堆叠吸附
        /// </summary>
        public static bool TrySnapToWall(
            Vector3 candidateGroundPos,
            CwcPlacementSettings settings,
            out Vector3 snappedPos,
            out Vector3 wallNormal,
            float footprintRadius = 0f)
        {
            settings = settings ?? CwcSceneDirectorSettings.GlobalPlacementSettings;
            LayerMask wallMask = settings.EffectiveWallLayer;
            if (wallMask == 0)
            {
                snappedPos = candidateGroundPos;
                wallNormal = Vector3.forward;
                return false;
            }

            Vector3 rayOrigin = candidateGroundPos + Vector3.up * 0.5f;
            float closestDist = float.MaxValue;
            RaycastHit closestHit = default;
            bool foundWall = false;

            // 水平 8 个方向发射探测射线探测实体墙壁
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                if (Physics.Raycast(rayOrigin, dir, out RaycastHit hit, settings.WallSearchDistance, wallMask))
                {
                    if (hit.distance < closestDist)
                    {
                        closestDist = hit.distance;
                        closestHit = hit;
                        foundWall = true;
                    }
                }
            }

            if (foundWall)
            {
                wallNormal = new Vector3(closestHit.normal.x, 0f, closestHit.normal.z).normalized;
                if (wallNormal.sqrMagnitude < 0.001f)
                {
                    wallNormal = Vector3.forward;
                }

                // 若指定底盘半径则按自身尺寸严丝合缝推开，否则按配置默认安全边距
                float offset = footprintRadius > 0.05f
                    ? Mathf.Max(0.2f, footprintRadius + 0.05f)
                    : settings.WallOffset;

                snappedPos = closestHit.point + wallNormal * offset;
                snappedPos.y = candidateGroundPos.y;

                // 二次确认吸附点是否仍有地面支撑
                if (TrySnapToGround(snappedPos, settings, out Vector3 validGround))
                {
                    snappedPos = validGround;
                    return true;
                }
            }

            snappedPos = candidateGroundPos;
            wallNormal = Vector3.forward;
            return false;
        }

        /// <summary>
        /// 靠墙探测兼容重载方法
        /// </summary>
        public static bool TrySnapToWall(
            Vector3 candidateGroundPos,
            float footprintRadius,
            CwcPlacementSettings settings,
            out Vector3 snappedPos,
            out Vector3 wallNormal)
        {
            return TrySnapToWall(candidateGroundPos, settings, out snappedPos, out wallNormal, footprintRadius);
        }

        /// <summary>
        /// 开阔地探测：检查周围指定距离内是否完全无墙体阻挡
        /// </summary>
        public static bool IsOpenSpace(Vector3 point, CwcPlacementSettings settings)
        {
            settings = settings ?? CwcSceneDirectorSettings.GlobalPlacementSettings;
            LayerMask wallMask = settings.EffectiveWallLayer;
            if (wallMask == 0) return true;

            Vector3 rayOrigin = point + Vector3.up * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                if (Physics.Raycast(rayOrigin, dir, settings.OpenSpaceMinWallDistance, wallMask))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region 朝向决策计算 (Orientation Calculation)

        /// <summary>
        /// 根据模式与墙壁法线计算最终朝向
        /// </summary>
        public static Quaternion CalculateOrientation(Vector3 wallNormal, PlacementMode mode)
        {
            if (mode == PlacementMode.WallSnapped && wallNormal.sqrMagnitude > 0.001f)
            {
                // 背靠墙壁，面向室内开阔空间
                return Quaternion.LookRotation(new Vector3(wallNormal.x, 0f, wallNormal.z));
            }

            // 自由或开阔地模式：随机水平朝向
            return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        #endregion
    }
}
