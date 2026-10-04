using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 场景空间拓扑感知与网格管理器（独立、平级、高内聚基础设施）
    /// 负责以 2.5D 世界分块（Spatial Chunk）为基准，按需懒加载探测场景地貌、邻墙法线与开阔度语义；
    /// 提供毫秒级空间查询、局部增量补全、局部刷新与最远点均匀采样（FPS）支持
    /// </summary>
    [AddComponentMenu("Cwcbb/Scene Director/Cwc Scene Spatial Manager")]
    public class CwcSceneSpatialManager : MonoBehaviour
    {
        #region 常量与静态成员 (Constants & Static)

        private const string LogPrefix = "[CwcSceneSpatialManager] ";
        private const string DefaultGameObjectName = "[CwcSceneSpatialManager]";

        private static CwcSceneSpatialManager _instance;
        private static bool _isApplicationQuitting = false;
        private static float[] s_MinDistBuffer = new float[256];
        private static float[] s_ScoreBuffer = new float[256];
        private static readonly List<CwcSpatialCell> s_CandidateBuffer = new List<CwcSpatialCell>(256);
        private static readonly List<CwcSpatialCell> s_FilteredPool = new List<CwcSpatialCell>(256);
        private static readonly List<Vector3> s_SpaciousResultsBuffer = new List<Vector3>(8);

        #endregion

        #region Inspector 序列化字段 (Serialized Fields)

        [Header("分块与精度设置 (Chunk & Cell Grid)")]
        [Tooltip("世界空间划分的固定分块大小（米，建议 24m ~ 32m）")]
        [SerializeField] private float _chunkSize = 32f;

        [Tooltip("高精度地表下沉采样步长（米，建议 1.0m ~ 1.5m 高分辨率步长，消除门洞与走廊盲区）")]
        [SerializeField] private float _cellSize = 1.2f;

        [Header("场景可视化调试 (Gizmos)")]
        [Tooltip("是否在选中此物体时在 Scene 视图中绘制已加载 Chunk 与高密地表点云")]
        [SerializeField] private bool _enableGizmos = true;

        #endregion

        #region 私有非序列化字段 (Private Fields)

        private readonly Dictionary<Vector2Int, CwcSpatialChunk> _chunks = new Dictionary<Vector2Int, CwcSpatialChunk>(32);
        private readonly List<Vector2Int> _cachedIntersectingCoords = new List<Vector2Int>(16);

        #endregion

        #region 公开属性 (Public Properties)

        public float ChunkSize => _chunkSize;
        public float CellSize => _cellSize;
        public int LoadedChunkCount => _chunks.Count;

        public static bool IsApplicationQuitting => _isApplicationQuitting;
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// 空间管理器场景单例访问点，场景中无节点时自动平滑懒创建独立节点
        /// </summary>
        public static CwcSceneSpatialManager Instance
        {
            get
            {
                if (_isApplicationQuitting) return null;

                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _instance = FindFirstObjectByType<CwcSceneSpatialManager>();
#else
                    _instance = FindObjectOfType<CwcSceneSpatialManager>();
#endif
                    if (_instance == null && !_isApplicationQuitting)
                    {
                        GameObject go = new GameObject(DefaultGameObjectName);
                        _instance = go.AddComponent<CwcSceneSpatialManager>();
                        Debug.Log(LogPrefix + "检测到当前场景未配置空间管理器节点，已自动创建 [" + DefaultGameObjectName + "] 实例。");
                    }
                }

                return _instance;
            }
        }

        #endregion

        #region Unity 生命周期 (Lifecycle)

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Debug.LogWarning(LogPrefix + "场景中存在重复的 CwcSceneSpatialManager，已自动销毁冗余组件。");
                Destroy(this);
            }
        }

        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            ClearAll();
        }

        private void OnDrawGizmosSelected()
        {
            if (_enableGizmos)
            {
                DrawSpatialGizmos();
            }
        }

        #endregion

        #region 公开核心查询与操作方法 (Public Methods)

        /// <summary>
        /// 按圆形区域查询空间网格单元，未扫描的 Chunk 自动按需增量补充，已扫描的走高速缓存
        /// </summary>
        public void GetCells(
            Vector3 center,
            float radius,
            CwcSpatialCellType filterMask,
            List<CwcSpatialCell> results,
            CwcPlacementSettings settingsOverride = null)
        {
            if (results == null) return;
            results.Clear();

            if (radius <= 0f) return;

            var settings = CwcSceneDirectorSettings.GetEffectivePlacementSettings(settingsOverride);
            float safeRadius = Mathf.Max(0.5f, radius);
            float radiusSq = safeRadius * safeRadius;

            // 1. 计算覆盖的分块坐标范围
            _cachedIntersectingCoords.Clear();
            GetIntersectingChunkCoords(center, safeRadius, _cachedIntersectingCoords);

            // 2. 确保所有涉及的分块均已懒加载完成探测
            int coordCount = _cachedIntersectingCoords.Count;
            for (int i = 0; i < coordCount; i++)
            {
                Vector2Int coord = _cachedIntersectingCoords[i];
                EnsureChunkLoaded(coord, settings, center.y);

                if (_chunks.TryGetValue(coord, out var chunk))
                {
                    chunk.CollectMatchingCells(center, radiusSq, filterMask, results);
                }
            }
        }

        /// <summary>
        /// 最远点采样（Farthest Point Sampling, FPS）
        /// 在离散网格候选池中，以最大空间离散度挑出 targetCount 个分布绝对均匀的坐标点
        /// 天然跨房间均衡扩散，杜绝单点扎堆，无需手动调配排斥半径
        /// </summary>
        public void SampleFarthestPoints(
            List<CwcSpatialCell> candidatePool,
            int targetCount,
            List<Vector3> results)
        {
            if (results == null) return;
            results.Clear();

            if (candidatePool == null || candidatePool.Count == 0 || targetCount <= 0) return;

            int n = candidatePool.Count;
            int k = Mathf.Min(targetCount, n);

            if (s_MinDistBuffer.Length < n)
            {
                s_MinDistBuffer = new float[Mathf.Max(s_MinDistBuffer.Length * 2, n)];
            }

            // 初始化各候选点到已选点集的最短距离平方为无穷大
            for (int i = 0; i < n; i++)
            {
                s_MinDistBuffer[i] = float.MaxValue;
            }

            // 1. 第一个点随机抽取
            int firstIndex = UnityEngine.Random.Range(0, n);
            Vector3 lastSelectedPos = candidatePool[firstIndex].Position;
            results.Add(lastSelectedPos);

            // 2. 迭代挑出剩余 k - 1 个使得“最小距离最大”的最远点
            for (int step = 1; step < k; step++)
            {
                float maxMinDistSq = -1f;
                int bestCandidateIndex = -1;

                for (int i = 0; i < n; i++)
                {
                    Vector3 candPos = candidatePool[i].Position;
                    float dx = candPos.x - lastSelectedPos.x;
                    float dz = candPos.z - lastSelectedPos.z;
                    float distSq = dx * dx + dz * dz;

                    if (distSq < s_MinDistBuffer[i])
                    {
                        s_MinDistBuffer[i] = distSq;
                    }

                    if (s_MinDistBuffer[i] > maxMinDistSq)
                    {
                        maxMinDistSq = s_MinDistBuffer[i];
                        bestCandidateIndex = i;
                    }
                }

                if (bestCandidateIndex == -1) break;

                lastSelectedPos = candidatePool[bestCandidateIndex].Position;
                results.Add(lastSelectedPos);
            }
        }

        /// <summary>
        /// 便捷单中心采样：在指定环形范围内，挑选出一个容纳尺寸最大、腹地最宽阔的理想中心坐标
        /// （纯点云拓扑分析，零物理射线开销，专用于敌群降临、Boss刷怪点）
        /// </summary>
        public bool TrySampleSpaciousCenter(
            Vector3 searchOrigin,
            float minRadius,
            float maxRadius,
            float requiredSpreadRadius,
            out Vector3 bestCenter,
            CwcPlacementSettings settingsOverride = null)
        {
            bestCenter = searchOrigin;
            s_SpaciousResultsBuffer.Clear();

            SampleSpaciousPoints(
                searchOrigin,
                minRadius,
                maxRadius,
                requiredSpreadRadius,
                1,
                s_SpaciousResultsBuffer,
                settingsOverride);

            if (s_SpaciousResultsBuffer.Count > 0)
            {
                bestCenter = s_SpaciousResultsBuffer[0];
                return true;
            }

            return false;
        }

        /// <summary>
        /// 开阔度加权最远点采样（Spacious-FPS）：
        /// 在环形候选点云中，结合“局部地面点云饱满度（代表腹地大小）”与“空间最远点离散度”，
        /// 挑出 targetCount 个分布绝对均匀、且每一个都处于大房间大腹地正中心的理想降临点！
        /// 彻底消除狭窄走廊卡位与多房间扎堆问题，全程纯数学运算，零物理检测
        /// </summary>
        public void SampleSpaciousPoints(
            Vector3 searchOrigin,
            float minRadius,
            float maxRadius,
            float requiredSpreadRadius,
            int targetCount,
            List<Vector3> results,
            CwcPlacementSettings settingsOverride = null)
        {
            if (results == null) return;
            results.Clear();

            if (maxRadius <= 0f || targetCount <= 0) return;

            // 1. 获取外环半径内的所有高密地面格点
            s_CandidateBuffer.Clear();
            GetCells(searchOrigin, maxRadius, CwcSpatialCellType.Walkable, s_CandidateBuffer, settingsOverride);

            if (s_CandidateBuffer.Count == 0) return;

            // 2. 环形区间初筛：过滤掉内环区域（防贴脸）
            float minRadSq = minRadius * minRadius;
            s_FilteredPool.Clear();
            int allCount = s_CandidateBuffer.Count;

            for (int i = 0; i < allCount; i++)
            {
                var cell = s_CandidateBuffer[i];
                float dx = cell.Position.x - searchOrigin.x;
                float dz = cell.Position.z - searchOrigin.z;
                float distSq = dx * dx + dz * dz;

                if (distSq >= minRadSq)
                {
                    s_FilteredPool.Add(cell);
                }
            }

            // 若环形过滤后无点，平滑回退使用全部候选点
            var pool = s_FilteredPool.Count > 0 ? s_FilteredPool : s_CandidateBuffer;
            int n = pool.Count;
            int k = Mathf.Min(targetCount, n);

            // 3. 计算各候选点的“开阔腹地得分（Spacious Score）”
            // 原理：以候选点为中心、在 requiredSpreadRadius 半径内能圈住的合法地面点越多，说明越处于大房间腹地中心
            if (s_ScoreBuffer.Length < n)
            {
                s_ScoreBuffer = new float[Mathf.Max(s_ScoreBuffer.Length * 2, n)];
            }

            float spreadRadius = Mathf.Max(1.0f, requiredSpreadRadius);
            float spreadSq = spreadRadius * spreadRadius;
            float maxScore = 1f;

            for (int i = 0; i < n; i++)
            {
                Vector3 posA = pool[i].Position;
                int countInRange = 0;

                // 统计周围 spreadRadius 范围内的地面点数
                for (int j = 0; j < n; j++)
                {
                    Vector3 posB = pool[j].Position;
                    float dx = posB.x - posA.x;
                    float dz = posB.z - posA.z;
                    if ((dx * dx + dz * dz) <= spreadSq)
                    {
                        countInRange++;
                    }
                }

                s_ScoreBuffer[i] = countInRange;
                if (countInRange > maxScore)
                {
                    maxScore = countInRange;
                }
            }

            // 4. 加权最远点采样（Weighted FPS）
            if (s_MinDistBuffer.Length < n)
            {
                s_MinDistBuffer = new float[Mathf.Max(s_MinDistBuffer.Length * 2, n)];
            }

            for (int i = 0; i < n; i++)
            {
                s_MinDistBuffer[i] = float.MaxValue;
            }

            // 4.1 第 1 个点：挑出环形范围内开阔度得分最高（腹地最平坦开阔）的点
            int bestFirstIdx = 0;
            float bestFirstScore = -1f;
            for (int i = 0; i < n; i++)
            {
                if (s_ScoreBuffer[i] > bestFirstScore)
                {
                    bestFirstScore = s_ScoreBuffer[i];
                    bestFirstIdx = i;
                }
            }

            Vector3 lastSelectedPos = pool[bestFirstIdx].Position;
            results.Add(lastSelectedPos);

            // 4.2 若只需要 1 个点，直接完成返回
            if (k == 1) return;

            // 4.3 迭代挑出剩余 k - 1 个使得 (minDistSq * normalizedScore) 最大的跨房间大腹地中心
            float invMaxScore = 1f / maxScore;

            for (int step = 1; step < k; step++)
            {
                float bestWeightedDist = -1f;
                int bestCandidateIndex = -1;

                for (int i = 0; i < n; i++)
                {
                    Vector3 candPos = pool[i].Position;
                    float dx = candPos.x - lastSelectedPos.x;
                    float dz = candPos.z - lastSelectedPos.z;
                    float distSq = dx * dx + dz * dz;

                    if (distSq < s_MinDistBuffer[i])
                    {
                        s_MinDistBuffer[i] = distSq;
                    }

                    // 开阔度权重占比（归一化得分 0.2 ~ 1.0），既保证离散距离足够远，又倾向于选择大腹地
                    float normalizedScore = Mathf.Clamp(s_ScoreBuffer[i] * invMaxScore, 0.2f, 1.0f);
                    float weightedDist = s_MinDistBuffer[i] * normalizedScore;

                    if (weightedDist > bestWeightedDist)
                    {
                        bestWeightedDist = weightedDist;
                        bestCandidateIndex = i;
                    }
                }

                if (bestCandidateIndex == -1) break;

                lastSelectedPos = pool[bestCandidateIndex].Position;
                results.Add(lastSelectedPos);
            }
        }

        /// <summary>
        /// 局部区域主动失效：将与圆形区域相交的分块从缓存中剔除，下次访问时将自动重新探测
        /// 常用于场景爆破破坏、墙体开启或动态障碍物更新后的局部更新
        /// </summary>
        public void InvalidateArea(Vector3 center, float radius)
        {
            if (radius <= 0f) return;

            var coords = new List<Vector2Int>(8);
            GetIntersectingChunkCoords(center, radius, coords);

            int count = coords.Count;
            for (int i = 0; i < count; i++)
            {
                InvalidateChunk(coords[i]);
            }
        }

        /// <summary>
        /// 使得特定分块失效
        /// </summary>
        public void InvalidateChunk(Vector2Int chunkCoord)
        {
            if (_chunks.Remove(chunkCoord))
            {
                Debug.Log(LogPrefix + "已将空间分块 [" + chunkCoord.x + ", " + chunkCoord.y + "] 标记失效。");
            }
        }

        /// <summary>
        /// 清空所有已加载的分块缓存
        /// </summary>
        public void ClearAll()
        {
            _chunks.Clear();
        }

        #endregion

        #region 私有几何与探测逻辑 (Private Methods)

        private void GetIntersectingChunkCoords(Vector3 center, float radius, List<Vector2Int> outputList)
        {
            int minX = Mathf.FloorToInt((center.x - radius) / _chunkSize);
            int maxX = Mathf.FloorToInt((center.x + radius) / _chunkSize);
            int minZ = Mathf.FloorToInt((center.z - radius) / _chunkSize);
            int maxZ = Mathf.FloorToInt((center.z + radius) / _chunkSize);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    outputList.Add(new Vector2Int(x, z));
                }
            }
        }

        private void EnsureChunkLoaded(Vector2Int coord, CwcPlacementSettings settings, float baselineHeight = 0f)
        {
            if (_chunks.ContainsKey(coord)) return;

            // 1. 构建该分块的世界 AABB 包围盒（垂直高度根据探测距离与基准面扩展）
            float minX = coord.x * _chunkSize;
            float minZ = coord.y * _chunkSize;
            Vector3 chunkCenter = new Vector3(minX + _chunkSize * 0.5f, baselineHeight, minZ + _chunkSize * 0.5f);
            Vector3 chunkSizeVec = new Vector3(_chunkSize, settings.MaxRaycastDistance * 2f, _chunkSize);
            Bounds bounds = new Bounds(chunkCenter, chunkSizeVec);

            // 步长为 1.0m~1.2m 时，32m Chunk 约有 700~1000 个地面格点，初始容量预分配 512
            var newChunk = new CwcSpatialChunk(coord, bounds, 512);

            // 2. 在 XZ 平面按高精度 _cellSize 步长密集发射垂直探针（数字降雨法）
            float halfCell = _cellSize * 0.5f;
            float startX = minX + halfCell;
            float endX = minX + _chunkSize;
            float startZ = minZ + halfCell;
            float endZ = minZ + _chunkSize;

            LayerMask obstacleMask = settings.EffectiveObstacleLayer;

            for (float x = startX; x < endX; x += _cellSize)
            {
                for (float z = startZ; z < endZ; z += _cellSize)
                {
                    Vector3 candidatePos = new Vector3(x, baselineHeight, z);

                    // 2.1 垂直降雨：下沉探测地面精准高度 Y（自动过滤虚空深渊与垂直陡峭立面）
                    if (!CwcPlacementSpatialUtil.TrySnapToGround(candidatePos, settings, out Vector3 groundPoint))
                    {
                        continue;
                    }

                    // 2.2 地面与头顶净空避障校验：
                    // a) 地面微观探针避障（0.15m 半径），剔除嵌在地面障碍内部的死点
                    if (!CwcPlacementSpatialUtil.IsObstacleFree(groundPoint, 0.15f, settings))
                    {
                        continue;
                    }

                    // b) 头顶净空检测（离地 0.5m 处 0.2m 球体），排除深陷在实心大柱体或墙体内部的伪地面
                    if (obstacleMask != 0 && Physics.CheckSphere(groundPoint + Vector3.up * 0.5f, 0.2f, obstacleMask))
                    {
                        continue;
                    }

                    // 2.3 收纳高精度纯净地面点（紧贴墙根且平整可行走）
                    var cell = new CwcSpatialCell(groundPoint, coord, CwcSpatialCellType.Walkable);
                    newChunk.AddCell(cell);
                }
            }

            _chunks.Add(coord, newChunk);
        }

        private void DrawSpatialGizmos()
        {
            if (_chunks.Count == 0) return;

            foreach (var kvp in _chunks)
            {
                var chunk = kvp.Value;

                // 绘制 Chunk 边界线框（半透明白）
                Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
                Gizmos.DrawWireCube(chunk.WorldBounds.center, chunk.WorldBounds.size);

                // 绘制高密地表点云（青绿色微球，清晰展现场景真实地表起伏与边界）
                Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.35f);
                var cells = chunk.AllCells;
                int count = cells.Count;
                for (int i = 0; i < count; i++)
                {
                    Gizmos.DrawSphere(cells[i].Position + Vector3.up * 0.05f, 0.12f);
                }
            }
        }

        #endregion
    }
}
