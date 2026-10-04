using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 基础出池放置载体契约
    /// 加权单次摆放模组与动态信用点模组的公共基础
    /// 载体完全自描述其放置策略（IPlacementStrategy）、物理底盘占用半径（FootprintRadius）与散开半径
    /// </summary>
    public interface ISpawnPayload
    {
        /// <summary>
        /// 本项单批次需要申请的落点数量（交互物一般为 1，小队可能为 3~5）
        /// </summary>
        int UnitCount { get; }

        /// <summary>
        /// 多态空间放置策略（自由地面 / 贴墙背墙 / 开阔腹地或自定义策略）
        /// </summary>
        IPlacementStrategy Strategy { get; }

        /// <summary>
        /// 物体物理底盘占用半径（米，由物体原生 Collider 碰撞体提供）
        /// 统一驱动悬崖防空边距、贴墙安全推开、障碍穿模检测与实体间距互斥
        /// </summary>
        float FootprintRadius { get; }

        /// <summary>
        /// 微观散开半径（米，由 Payload 自描述，如小队根据编制人数自适应）
        /// </summary>
        float SpreadRadius { get; }

        /// <summary>
        /// 编辑器检视面板中的紧凑信息预览文本
        /// 遵循开闭原则由载体自描述，使通用调度外壳编辑器完全解耦具体的 Payload 实现
        /// </summary>
        string SummaryText { get; }

        /// <summary>
        /// 具体的出池、位姿对齐、重置与激活协程流水线
        /// </summary>
        IEnumerator SpawnRoutine(IReadOnlyList<Pose> poses, CwcPlacementContext context);
    }

    /// <summary>
    /// 动态信用点刷怪载体契约（专用于动态信用点导演模组）
    /// 战力开销（TotalCost）由载体自身内部固定单体 BaseCost 动态汇总计算，外壳无需且不能硬编码
    /// </summary>
    public interface ICreditSpawnPayload : ISpawnPayload
    {
        /// <summary>
        /// 真实战力开销（Threat Cost）
        /// 由载体内部根据所包含兵种单位的 BaseCost 动态计算得出，作为单一真实数据源
        /// </summary>
        int TotalCost { get; }
    }
}
