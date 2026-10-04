using System;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 空间网格单元语义类型标记（支持位运算组合）
    /// </summary>
    [Flags]
    public enum CwcSpatialCellType
    {
        None = 0,
        Walkable = 1 << 0,       // 合法可行走平整地面（核心基准标记）
        [Obsolete("墙体判定已移交具体摆放项就地探测")]
        WallAdjacent = 1 << 1,   // 兼容保留标记
        [Obsolete("拐角判定已移交具体摆放项就地探测")]
        Corner = 1 << 2,         // 兼容保留标记
        [Obsolete("开阔地判定已移交具体摆放项就地探测")]
        OpenFloor = 1 << 3,      // 兼容保留标记
        [Obsolete("通道判定已移交具体摆放项就地探测")]
        Chokepoint = 1 << 4      // 兼容保留标记
    }

    /// <summary>
    /// 2.5D 高密度空间地面网格单元数据（只读极轻结构体）
    /// 记录世界坐标（含精确垂直降雨探测地面高度 Y）与所属分块坐标，纯零 GC 开销
    /// </summary>
    [Serializable]
    public readonly struct CwcSpatialCell : IEquatable<CwcSpatialCell>
    {
        #region 公开只读属性 (Public Properties)

        /// <summary>
        /// 该网格单元在世界空间中的精准着陆点（XZ 为网格平面采样点，Y 为垂直降雨探测的真实物理地面高度）
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        /// 所属世界分块二维坐标
        /// </summary>
        public Vector2Int ChunkCoord { get; }

        /// <summary>
        /// 空间网格单元类型
        /// </summary>
        public CwcSpatialCellType Type { get; }

        /// <summary>
        /// 兼容保留只读属性（墙体法线由具体摆放项按需就地探测）
        /// </summary>
        public Vector3 WallNormal => Vector3.zero;

        /// <summary>
        /// 兼容保留只读属性
        /// </summary>
        public float ClearanceDistance => 1.0f;

        #endregion

        #region 构造函数 (Constructors)

        public CwcSpatialCell(
            Vector3 position,
            Vector2Int chunkCoord,
            CwcSpatialCellType type = CwcSpatialCellType.Walkable)
        {
            Position = position;
            ChunkCoord = chunkCoord;
            Type = type;
        }

        [Obsolete("已简化构造函数，推荐使用 (position, chunkCoord, type)")]
        public CwcSpatialCell(
            Vector3 position,
            Vector3 wallNormal,
            CwcSpatialCellType type,
            Vector2Int chunkCoord,
            float clearanceDistance)
        {
            Position = position;
            ChunkCoord = chunkCoord;
            Type = type;
        }

        #endregion

        #region 相等性判断 (Equality)

        public bool Equals(CwcSpatialCell other)
        {
            return Position.Equals(other.Position) && ChunkCoord.Equals(other.ChunkCoord);
        }

        public override bool Equals(object obj)
        {
            return obj is CwcSpatialCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Position.GetHashCode() * 397) ^ ChunkCoord.GetHashCode();
            }
        }

        public static bool operator ==(CwcSpatialCell left, CwcSpatialCell right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CwcSpatialCell left, CwcSpatialCell right)
        {
            return !left.Equals(right);
        }

        #endregion
    }
}
