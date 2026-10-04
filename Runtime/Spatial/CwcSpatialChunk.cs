using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 空间网格世界分块数据容器（纯 C# 类）
    /// 维护单一分块区域（如 32m x 32m）内的所有有效地面网格单元及其快速分类池
    /// </summary>
    public class CwcSpatialChunk
    {
        #region 私有字段 (Private Fields)

        private readonly Vector2Int _coord;
        private readonly Bounds _worldBounds;
        private readonly List<CwcSpatialCell> _allCells;

        #endregion

        #region 公开属性 (Public Properties)

        public Vector2Int Coord => _coord;
        public Bounds WorldBounds => _worldBounds;
        public IReadOnlyList<CwcSpatialCell> AllCells => _allCells;
        public int TotalCount => _allCells.Count;

        #endregion

        #region 构造函数 (Constructors)

        public CwcSpatialChunk(Vector2Int coord, Bounds worldBounds, int initialCapacity = 256)
        {
            _coord = coord;
            _worldBounds = worldBounds;
            _allCells = new List<CwcSpatialCell>(initialCapacity);
        }

        #endregion

        #region 公开方法 (Public Methods)

        /// <summary>
        /// 向当前分块添加一个探测到的地面网格单元
        /// </summary>
        public void AddCell(CwcSpatialCell cell)
        {
            _allCells.Add(cell);
        }

        /// <summary>
        /// 按指定的语义位掩码与圆形范围，将匹配的单元填充到外部结果列表中
        /// </summary>
        public void CollectMatchingCells(
            Vector3 center,
            float radiusSq,
            CwcSpatialCellType filterMask,
            List<CwcSpatialCell> outputList)
        {
            if (outputList == null) return;

            int count = _allCells.Count;
            for (int i = 0; i < count; i++)
            {
                var cell = _allCells[i];

                if (filterMask != CwcSpatialCellType.None && (cell.Type & filterMask) == 0)
                {
                    continue;
                }

                // 计算 X-Z 平面距离平方（2.5D 高度场以平面距离判定范围）
                float dx = cell.Position.x - center.x;
                float dz = cell.Position.z - center.z;
                if ((dx * dx + dz * dz) <= radiusSq)
                {
                    outputList.Add(cell);
                }
            }
        }

        #endregion
    }
}
