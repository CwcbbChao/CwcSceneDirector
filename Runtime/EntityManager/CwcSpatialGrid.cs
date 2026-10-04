using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 基于 X-Z 水平面的轻量 2D 空间哈希网格系统
    /// 用于以接近 O(1) 的复杂度快速查询区域内的实体及威胁消耗总和，杜绝全场遍历与 GC
    /// </summary>
    public class CwcSpatialGrid
    {
        #region 常量与静态成员 (Constants & Static)

        public const float DefaultCellSize = 15f;

        #endregion

        #region 私有字段 (Private Fields)

        private readonly float _cellSize;
        private readonly float _inverseCellSize;
        private readonly Dictionary<Vector2Int, List<CwcSceneEntityHook>> _grid;
        private readonly Stack<List<CwcSceneEntityHook>> _listPool;

        #endregion

        #region 公开属性 (Public Properties)

        public float CellSize => _cellSize;
        public int CellCount => _grid.Count;

        #endregion

        #region 构造函数与重置方法 (Constructors & Public Methods)

        public CwcSpatialGrid(float cellSize = DefaultCellSize)
        {
            _cellSize = cellSize > 1f ? cellSize : DefaultCellSize;
            _inverseCellSize = 1f / _cellSize;
            _grid = new Dictionary<Vector2Int, List<CwcSceneEntityHook>>(64);
            _listPool = new Stack<List<CwcSceneEntityHook>>(16);
        }

        /// <summary>
        /// 将实体插入到空间网格
        /// </summary>
        public void Insert(CwcSceneEntityHook hook)
        {
            if (hook == null) return;

            Vector2Int coord = WorldToCellCoord(hook.transform.position);
            hook.SetGridCellCoord(coord);

            if (!_grid.TryGetValue(coord, out var list))
            {
                list = GetOrCreateList();
                _grid.Add(coord, list);
            }

            if (!list.Contains(hook))
            {
                list.Add(hook);
            }
        }

        /// <summary>
        /// 从空间网格中移除实体
        /// </summary>
        public void Remove(CwcSceneEntityHook hook)
        {
            if (hook == null) return;

            Vector2Int coord = hook.GridCellCoord;
            if (_grid.TryGetValue(coord, out var list))
            {
                list.Remove(hook);
                if (list.Count == 0)
                {
                    _grid.Remove(coord);
                    RecycleList(list);
                }
            }
        }

        /// <summary>
        /// 更新实体的空间网格坐标（若跨网格则自动迁移）
        /// </summary>
        public void UpdatePosition(CwcSceneEntityHook hook)
        {
            if (hook == null) return;

            Vector2Int newCoord = WorldToCellCoord(hook.transform.position);
            Vector2Int oldCoord = hook.GridCellCoord;

            if (newCoord == oldCoord) return;

            // 从旧网格移除
            if (_grid.TryGetValue(oldCoord, out var oldList))
            {
                oldList.Remove(hook);
                if (oldList.Count == 0)
                {
                    _grid.Remove(oldCoord);
                    RecycleList(oldList);
                }
            }

            // 加入新网格
            hook.SetGridCellCoord(newCoord);
            if (!_grid.TryGetValue(newCoord, out var newList))
            {
                newList = GetOrCreateList();
                _grid.Add(newCoord, newList);
            }

            if (!newList.Contains(hook))
            {
                newList.Add(hook);
            }
        }

        /// <summary>
        /// 查询指定中心点与半径范围内的所有实体（考虑 3D 球形距离，零 GC 填充至结果列表）
        /// </summary>
        public void QueryRadius(Vector3 center, float radius, List<CwcSceneEntityHook> results)
        {
            QueryRadius(center, radius, float.MaxValue, results);
        }

        /// <summary>
        /// 查询指定中心点、水平半径与最大垂直高度差范围内的所有实体（零 GC 填充至结果列表）
        /// </summary>
        public void QueryRadius(Vector3 center, float radius, float maxHeightDiff, List<CwcSceneEntityHook> results)
        {
            if (results == null) return;

            results.Clear();
            float radiusSq = radius * radius;

            int minX = Mathf.FloorToInt((center.x - radius) * _inverseCellSize);
            int maxX = Mathf.FloorToInt((center.x + radius) * _inverseCellSize);
            int minZ = Mathf.FloorToInt((center.z - radius) * _inverseCellSize);
            int maxZ = Mathf.FloorToInt((center.z + radius) * _inverseCellSize);

            Vector2Int coord = Vector2Int.zero;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    coord.x = x;
                    coord.y = z;

                    if (_grid.TryGetValue(coord, out var list))
                    {
                        int count = list.Count;
                        for (int i = 0; i < count; i++)
                        {
                            CwcSceneEntityHook hook = list[i];
                            if (hook == null) continue;

                            Vector3 pos = hook.transform.position;
                            float dy = pos.y - center.y;
                            if (Mathf.Abs(dy) > maxHeightDiff) continue;

                            float dx = pos.x - center.x;
                            float dz = pos.z - center.z;
                            float distSq = dx * dx + dy * dy + dz * dz;

                            if (distSq <= radiusSq)
                            {
                                results.Add(hook);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 统计指定中心点与半径范围内的活跃实体总威胁开销（考虑 3D 球形距离）
        /// </summary>
        public int GetTotalCostInRadius(Vector3 center, float radius)
        {
            return GetTotalCostInRadius(center, radius, float.MaxValue);
        }

        /// <summary>
        /// 统计指定中心点、水平半径与最大垂直高度差范围内的活跃实体总威胁开销
        /// </summary>
        public int GetTotalCostInRadius(Vector3 center, float radius, float maxHeightDiff)
        {
            float radiusSq = radius * radius;
            int totalCost = 0;

            int minX = Mathf.FloorToInt((center.x - radius) * _inverseCellSize);
            int maxX = Mathf.FloorToInt((center.x + radius) * _inverseCellSize);
            int minZ = Mathf.FloorToInt((center.z - radius) * _inverseCellSize);
            int maxZ = Mathf.FloorToInt((center.z + radius) * _inverseCellSize);

            Vector2Int coord = Vector2Int.zero;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    coord.x = x;
                    coord.y = z;

                    if (_grid.TryGetValue(coord, out var list))
                    {
                        int count = list.Count;
                        for (int i = 0; i < count; i++)
                        {
                            CwcSceneEntityHook hook = list[i];
                            if (hook == null) continue;

                            Vector3 pos = hook.transform.position;
                            float dy = pos.y - center.y;
                            if (Mathf.Abs(dy) > maxHeightDiff) continue;

                            float dx = pos.x - center.x;
                            float dz = pos.z - center.z;
                            float distSq = dx * dx + dy * dy + dz * dz;

                            if (distSq <= radiusSq)
                            {
                                totalCost += hook.ThreatCost;
                            }
                        }
                    }
                }
            }

            return totalCost;
        }

        /// <summary>
        /// 清空所有网格缓存
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _grid)
            {
                kvp.Value.Clear();
                RecycleList(kvp.Value);
            }
            _grid.Clear();
        }

        #endregion

        #region 私有辅助方法 (Private Methods)

        private Vector2Int WorldToCellCoord(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x * _inverseCellSize),
                Mathf.FloorToInt(worldPos.z * _inverseCellSize)
            );
        }

        private List<CwcSceneEntityHook> GetOrCreateList()
        {
            if (_listPool.Count > 0)
            {
                return _listPool.Pop();
            }
            return new List<CwcSceneEntityHook>(8);
        }

        private void RecycleList(List<CwcSceneEntityHook> list)
        {
            list.Clear();
            _listPool.Push(list);
        }

        #endregion
    }
}
