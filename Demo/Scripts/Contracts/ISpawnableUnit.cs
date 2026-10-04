using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 通用可生成单位契约（纯 C# 抽象，不绑定任何玩法）
    /// 任何具备生成预制体与固有基础强度（BaseCost）的数据类均可实现此契约
    /// </summary>
    public interface ISpawnableUnit
    {
        /// <summary>
        /// 实体对应的游戏对象预制体（用于从对象池提取）
        /// </summary>
        GameObject Prefab { get; }

        /// <summary>
        /// 单位固有的基础战力开销（Base Threat Cost）
        /// 全局唯一定义在单位自身，如小怪为 1，强力怪为 3
        /// </summary>
        int BaseCost { get; }
    }
}
