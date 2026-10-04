using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector
{
    /// <summary>
    /// 摆放生成运行时上下文
    /// 传递给各配置项（SpawnRoutine）以供访问总导演、单位管理器及全局设置
    /// </summary>
    public class CwcPlacementContext
    {
        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private float _frameBudgetMs = 2.0f;

        public CwcSceneDirector Director { get; }
        public CwcSceneEntityManager EntityManager { get; }
        public CwcPlacementSettings Settings { get; }
        public CwcSceneDirectorModule OwnerModule { get; }
        public int ClusterIndex { get; set; }
        public Vector3 CurrentClusterCenter { get; set; }

        /// <summary>
        /// 单帧耗时是否已超出预算上限（若超出应立即 yield return null 让出控制权）
        /// </summary>
        public bool ShouldYield => _stopwatch.Elapsed.TotalMilliseconds >= _frameBudgetMs;

        public CwcPlacementContext(
            CwcSceneDirector director,
            CwcSceneEntityManager entityManager,
            CwcPlacementSettings settings = null,
            float frameBudgetMs = -1f,
            CwcSceneDirectorModule ownerModule = null)
        {
            Director = director;
            EntityManager = entityManager;
            Settings = settings ?? CwcSceneDirectorSettings.GlobalPlacementSettings;
            _frameBudgetMs = frameBudgetMs > 0f ? frameBudgetMs : CwcSceneDirectorSettings.GlobalFrameBudgetMs;
            OwnerModule = ownerModule;
            _stopwatch.Start();
        }

        /// <summary>
        /// 重置当帧的高精度耗时统计（在 yield return null 恢复后调用）
        /// </summary>
        public void ResetFrameTimer()
        {
            _stopwatch.Restart();
        }
    }
}
