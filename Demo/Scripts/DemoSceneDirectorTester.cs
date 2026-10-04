using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Demo
{
    /// <summary>
    /// 场景导演运行时测试器 (MonoBehaviour)
    /// 演示如何直接在场景中引用配置资产，并在运行时激活交互物摆放与敌群动态刷怪
    /// </summary>
    [AddComponentMenu("Cwcbb/Demo/Scene Director Tester")]
    public class DemoSceneDirectorTester : MonoBehaviour
    {
        #region 常量与静态 (Constants & Static)

        private const string LogPrefix = "[DemoSceneDirectorTester] ";

        #endregion

        #region Inspector 序列化字段

        [Header("配置资产引用 (Config Asset)")]
        [Tooltip("拖入在 Project 视图中配置好的 DemoZoneConfigSO 资产")]
        [SerializeField] private DemoZoneConfigSO _zoneConfig;

        [Header("触发设置 (Trigger Settings)")]
        [Tooltip("是否在场景加载完毕 (Start) 时自动执行交互物摆放")]
        [SerializeField] private bool _spawnInteractablesOnStart = true;

        [Tooltip("是否在场景加载完毕 (Start) 时直接激活敌群遭遇战")]
        [SerializeField] private bool _startEncounterOnStart = false;

        [Header("键盘快捷键测试 (Keyboard Controls)")]
        [Tooltip("手动生成交互物的测试按键")]
        [SerializeField] private KeyCode _spawnInteractablesKey = KeyCode.I;

        [Tooltip("手动清空所有交互物宝箱的测试按键")]
        [SerializeField] private KeyCode _clearInteractablesKey = KeyCode.C;

        [Tooltip("手动启动敌群遭遇战的测试按键")]
        [SerializeField] private KeyCode _startEncounterKey = KeyCode.E;

        [Tooltip("手动停止当前遭遇战的测试按键")]
        [SerializeField] private KeyCode _stopEncounterKey = KeyCode.K;

        [Tooltip("一键清场测试按键（模拟杀光所有敌人）")]
        [SerializeField] private KeyCode _eliminateAllKey = KeyCode.X;

        [Header("空间选点模式 (Spatial Mode)")]
        [Tooltip("敌群生成是否优先以玩家当前站立位置为生成中心（为 false 则固定以本触发器 Transform 为中心）")]
        [SerializeField] private bool _usePlayerPositionAsCenter = true;

        [Tooltip("交互物宝箱是否使用地牢全图几何中心 Vector3(0, 0, 10) 摆放（为 false 则以玩家站立点为局部中心）")]
        [SerializeField] private bool _interactableUseMapCenter = true;

        [Header("空间采样算法选型 (Algorithm Mode)")]
        [Tooltip("是否优先使用场景空间拓扑感知与最远点采样（FPS，全图跨房间极致均匀）；为 false 则使用传统泊松盘算法")]
        [SerializeField] private bool _useSpatialGrid = true;

        [Header("运行时空间上下文参数 (Runtime Context)")]
        [Tooltip("交互物摆放影响半径（米，全图模式下 55m 完整覆盖整个地牢六大区域）")]
        [SerializeField] private float _interactableRadius = 55f;

        [Tooltip("交互物最多生成的群落/宝箱总数（泊松盘采样的候选点上限）")]
        [SerializeField] private int _interactableMaxClusters = 36;

        [Tooltip("交互物群落间最小排斥间距（米，防止宝箱彼此过于靠近密集）")]
        [SerializeField] private float _interactableMinDistance = 7.5f;

        [Tooltip("重新生成交互物时是否自动清空已有宝箱（方便测试单次分布均匀度）")]
        [SerializeField] private bool _clearChestsBeforeRespawn = true;

        [Tooltip("敌群生成最小环形安全半径（米，防止贴脸骑脸刷新）")]
        [SerializeField] private float _minEncounterRadius = 8f;

        [Tooltip("敌群遭遇战最大影响半径（米）")]
        [SerializeField] private float _encounterRadius = 22f;

        [Tooltip("遭遇战总战力预算 (TotalBudget，由关卡或祭坛注入)")]
        [SerializeField] private int _encounterBudget = 45;

        [Tooltip("同屏最大在场威胁度 (MaxConcurrentCost)")]
        [SerializeField] private int _maxConcurrentCost = 14;

        #endregion

        #region 私有非序列化字段

        private CwcWeightedPlacementModule _activeInteractableModule;
        private CwcCreditDirectorModule _activeEncounterModule;
        private Transform _playerTransform;
        private DemoPlayerController _playerController;

        #endregion

        #region 公开属性

        public DemoZoneConfigSO ZoneConfig
        {
            get => _zoneConfig;
            set => _zoneConfig = value;
        }

        public bool IsEncounterRunning => _activeEncounterModule != null && _activeEncounterModule.IsRunning;

        #endregion

        #region Unity 生命周期

        private void Start()
        {
            // 演示：确保全局物理环境设置与当前项目/场景图层匹配
            int groundLayer = LayerMask.NameToLayer("Ground");
            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            int interactableLayer = LayerMask.NameToLayer("Interactable");

            CwcSceneDirectorSettings.GroundLayer = groundLayer != -1 ? (1 << groundLayer) : (1 << 0);
            CwcSceneDirectorSettings.WallLayer = obstacleLayer != -1 ? (1 << obstacleLayer) : 0;

            LayerMask obsMask = 0;
            if (obstacleLayer != -1) obsMask |= (1 << obstacleLayer);
            if (interactableLayer != -1) obsMask |= (1 << interactableLayer);
            CwcSceneDirectorSettings.ObstacleLayer = obsMask;

            CwcSceneDirectorSettings.UseNavMesh = false;

            // 自动注册 Player 为关注目标，防止超距淘汰误杀或报错
            GameObject playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;
                _playerController = playerGo.GetComponent<DemoPlayerController>();
                CwcSceneEntityManager.Instance.AddFocusTarget(_playerTransform);
            }

            if (_zoneConfig == null)
            {
                Debug.LogWarning(LogPrefix + "未配置 DemoZoneConfigSO 资产，请在 Inspector 检视面板中拖入配置！");
                return;
            }

            Debug.Log(LogPrefix + "测试场景已就绪！可在屏幕左上角查看操作指引或按下 [I] / [E] / [K] 进行测试。");

            if (_spawnInteractablesOnStart)
            {
                TriggerInteractablePlacement();
            }

            if (_startEncounterOnStart)
            {
                TriggerEnemyEncounter();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(_spawnInteractablesKey))
            {
                TriggerInteractablePlacement();
            }

            if (Input.GetKeyDown(_clearInteractablesKey))
            {
                ClearActiveInteractables();
            }

            if (Input.GetKeyDown(_startEncounterKey))
            {
                TriggerEnemyEncounter();
            }

            if (Input.GetKeyDown(_stopEncounterKey))
            {
                StopEnemyEncounter();
            }

            if (Input.GetKeyDown(_eliminateAllKey))
            {
                EliminateAllActiveEntities();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && !IsEncounterRunning)
            {
                Debug.Log(LogPrefix + "检测到玩家进入触发器范围，自动激活遭遇战！");
                TriggerEnemyEncounter();
            }
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(15, 15, 410, 305), "Scene Director Dungeon Demo");

            GUILayout.BeginArea(new Rect(25, 40, 390, 275));

            string currentZone = GetCurrentZoneName();
            string encounterStatus = IsEncounterRunning ? "<color=green>Running</color>" : "<color=gray>Idle</color>";
            int activeCount = CwcSceneEntityManager.HasInstance ? CwcSceneEntityManager.Instance.ActiveEntityCount : 0;
            int currentCost = CwcSceneEntityManager.HasInstance ? CwcSceneEntityManager.Instance.TotalThreatCost : 0;
            int remainingBudget = _activeEncounterModule != null ? _activeEncounterModule.RemainingBudget : 0;
            string targetStatus = _usePlayerPositionAsCenter && _playerTransform != null ? "Player (Live)" : "Fixed Point";

            GameObject chestContainer = GameObject.Find("[Interactables]");
            int currentChestCount = chestContainer != null ? chestContainer.transform.childCount : 0;

            GUILayout.Label($"Zone: <b><color=cyan>{currentZone}</color></b>");
            GUILayout.Label($"Chests in Dungeon: <b><color=yellow>{currentChestCount}</color></b> | Plan: <b>{_interactableMaxClusters}</b> (Radius: {_interactableRadius}m)");
            GUILayout.Label($"Encounter: {encounterStatus} | Rem: <b>{remainingBudget}</b> | Threat: <b>{currentCost}/{_maxConcurrentCost}</b> (Count: {activeCount})");
            GUILayout.Label($"Tracking: <b><color=yellow>{targetStatus}</color></b> | Ring: <b>{_minEncounterRadius}m ~ {_encounterRadius}m</b>");

            string algoColor = _useSpatialGrid ? "green" : "orange";
            string algoText = _useSpatialGrid ? "Spatial Grid + FPS (Dispersed)" : "Legacy Poisson Disk";
            if (GUILayout.Button($"Algorithm: <b><color={algoColor}>{algoText}</color></b> (Click)", GUILayout.Height(20)))
            {
                _useSpatialGrid = !_useSpatialGrid;
            }
            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Spawn Chests ({_interactableMaxClusters}) [I]", GUILayout.Height(24)))
            {
                TriggerInteractablePlacement();
            }
            if (GUILayout.Button("Clear Chests [C]", GUILayout.Height(24)))
            {
                ClearActiveInteractables();
            }
            if (GUILayout.Button(IsEncounterRunning ? "Stop [K]" : "Encounter [E]", GUILayout.Height(24)))
            {
                if (IsEncounterRunning) StopEnemyEncounter();
                else TriggerEnemyEncounter();
            }
            if (GUILayout.Button("Kill [X]", GUILayout.Width(60), GUILayout.Height(24)))
            {
                EliminateAllActiveEntities();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("<b>Teleport to Dungeon Zone:</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Central Hall")) TeleportPlayerTo(new Vector3(0f, 1f, 0f));
            if (GUILayout.Button("Corridor")) TeleportPlayerTo(new Vector3(0f, 1f, 24f));
            if (GUILayout.Button("Sanctum")) TeleportPlayerTo(new Vector3(0f, 2f, 50f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("West Crypt")) TeleportPlayerTo(new Vector3(-33f, 1f, 0f));
            if (GUILayout.Button("East Pillars")) TeleportPlayerTo(new Vector3(34f, 1f, 0f));
            if (GUILayout.Button("Start Entry")) TeleportPlayerTo(new Vector3(0f, 1f, -30f));
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 chestCenter = _interactableUseMapCenter ? new Vector3(0f, 0f, 10f) : GetEffectiveCenter();

            // 绘制交互物全图摆放范围（青色线框）
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(chestCenter, _interactableRadius);

            Vector3 encounterCenter = GetEffectiveCenter();
            // 绘制敌群刷怪最小环形安全半径（橙黄色线框，防止贴脸）
            if (_minEncounterRadius > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.35f);
                Gizmos.DrawWireSphere(encounterCenter, _minEncounterRadius);
            }

            // 绘制敌群刷怪外环最大范围（红色线框）
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(encounterCenter, _encounterRadius);
        }

        #endregion

        #region 公开操作方法

        /// <summary>
        /// 获取当前生效的生成中心坐标（优先跟随玩家当前位置）
        /// </summary>
        public Vector3 GetEffectiveCenter()
        {
            if (_usePlayerPositionAsCenter && _playerTransform != null)
            {
                return _playerTransform.position;
            }
            return transform.position;
        }

        /// <summary>
        /// 根据当前位置判断所在的地牢区域名称
        /// </summary>
        public string GetCurrentZoneName()
        {
            Vector3 pos = GetEffectiveCenter();
            if (pos.z > 36f) return "North Sanctum (Elevated Platform)";
            if (pos.z >= 12f) return "North Corridor (Narrow Passage)";
            if (pos.x < -12f) return "West Crypt (Alcoves & Corners)";
            if (pos.x > 12f) return "East Pillar Hall (Dense Columns)";
            if (pos.z < -12f) return "South Entry Courtyard (Safe Start)";
            return "Central Hall (Open Arena)";
        }

        /// <summary>
        /// 安全传送玩家到目标世界坐标
        /// </summary>
        public void TeleportPlayerTo(Vector3 targetPos)
        {
            if (_playerController != null)
            {
                _playerController.TeleportTo(targetPos);
            }
            else if (_playerTransform != null)
            {
                _playerTransform.position = targetPos;
            }
        }

        /// <summary>
        /// 清空场景中已生成的所有交互物（清理 [Interactables] 容器下的子物体）
        /// </summary>
        public void ClearActiveInteractables()
        {
            GameObject container = GameObject.Find("[Interactables]");
            if (container != null)
            {
                int childCount = container.transform.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    var child = container.transform.GetChild(i);
                    Destroy(child.gameObject);
                }
                Debug.Log(LogPrefix + "已清空场景中所有交互物宝箱，共清除 " + childCount + " 个。");
            }
        }

        /// <summary>
        /// 激活交互物单次加权摆放（模拟全图离散均匀撒点）
        /// </summary>
        public void TriggerInteractablePlacement()
        {
            if (_zoneConfig == null || _zoneConfig.InteractableConfig == null)
            {
                Debug.LogError(LogPrefix + "激活失败：未配置有效的交互物配置项！");
                return;
            }

            // 若配置了重新摆放前清空旧箱子，先执行安全清理
            if (_clearChestsBeforeRespawn)
            {
                ClearActiveInteractables();
            }

            // 全图模式默认采用地牢几何中心 Vector3(0, 0, 10)，确保 55m 半径能均等辐射至六大区域；否则以局部为中心
            Vector3 center = _interactableUseMapCenter ? new Vector3(0f, 0f, 10f) : GetEffectiveCenter();
            Debug.Log(LogPrefix + "正在执行全图交互物加权摆放... 中心点: " + center + "，半径: " + _interactableRadius + "m，目标上限: " + _interactableMaxClusters + " 个，排斥间距: " + _interactableMinDistance + "m");

            // 1. 运行时动态构建空间上下文（全图半径、中心、群落数上限、最小排斥间距、空间拓扑感知开关）
            var context = new PlacementAreaContext(
                center: center,
                radius: _interactableRadius,
                maxClusters: _interactableMaxClusters,
                minClusterDistance: _interactableMinDistance,
                settingsOverride: null,
                useSpatialGrid: _useSpatialGrid
            );

            // 2. 结合纯净内容配置构建模块（物理设置自动回退到全局 CwcSceneDirectorSettings）
            _activeInteractableModule = _zoneConfig.InteractableConfig.CreateModule(context);

            // 3. 注册到导演，立即激活运行时分帧采样摆放
            CwcSceneDirector.Register(_activeInteractableModule);
        }

        /// <summary>
        /// 激活敌群动态遭遇战刷怪
        /// </summary>
        public void TriggerEnemyEncounter()
        {
            if (_zoneConfig == null || _zoneConfig.EncounterConfig == null)
            {
                Debug.LogError(LogPrefix + "激活失败：未配置有效的敌群遭遇战配置项！");
                return;
            }

            if (IsEncounterRunning)
            {
                Debug.LogWarning(LogPrefix + "当前遭遇战已在运行中，请勿重复触发！");
                return;
            }

            Vector3 center = GetEffectiveCenter();
            Transform target = _usePlayerPositionAsCenter ? _playerTransform : transform;
            Debug.Log(LogPrefix + "正在启动敌群动态狂潮遭遇战，初始中心：" + center + "，动态追踪目标：" + (target != null ? target.name : "None") + "，环形范围：" + _minEncounterRadius + "~" + _encounterRadius + "m");

            // 1. 运行时动态构建遭遇战上下文（中心、动态追踪目标、环形安全范围、总预算、并发上限、节奏等）
            var context = new EncounterContext(
                center: center,
                radius: _encounterRadius,
                totalBudget: _encounterBudget,
                maxConcurrentCost: _maxConcurrentCost,
                waveCooldown: 3.5f,
                settingsOverride: null,
                minRadius: _minEncounterRadius,
                centerTarget: target
            );

            // 2. 结合纯净内容配置构建模块（物理设置自动回退到全局 CwcSceneDirectorSettings）
            _activeEncounterModule = _zoneConfig.EncounterConfig.CreateModule(context);

            // 3. 监听关键生命周期事件
            _activeEncounterModule.OnBudgetDepleted += OnEncounterBudgetDepleted;
            _activeEncounterModule.OnClearedAndCompleted += OnEncounterClearedAndCompleted;

            // 4. 注册到导演，立即激活动态调度
            CwcSceneDirector.Register(_activeEncounterModule);
        }

        /// <summary>
        /// 手动停止当前遭遇战
        /// </summary>
        public void StopEnemyEncounter()
        {
            if (_activeEncounterModule != null && _activeEncounterModule.IsRunning)
            {
                Debug.Log(LogPrefix + "手动终止当前遭遇战。");
                _activeEncounterModule.Complete();
                _activeEncounterModule = null;
            }
        }

        /// <summary>
        /// 模拟消灭当前场上所有活跃实体（用于测试杀怪后导演立刻感知压力释放并刷出下一波）
        /// </summary>
        public void EliminateAllActiveEntities()
        {
            if (!CwcSceneEntityManager.HasInstance) return;

            var entities = CwcSceneEntityManager.Instance.ActiveEntities;
            // 倒序批量销毁回池
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                if (i < entities.Count && entities[i] != null)
                {
                    entities[i].Despawn();
                }
            }
            Debug.Log(LogPrefix + "已模拟消灭并回池当前场上所有活跃实体！压力瞬间释放，观察导演下一波出怪反应。");
        }

        #endregion

        #region 私有事件回调

        private void OnEncounterBudgetDepleted()
        {
            Debug.Log(LogPrefix + "狂潮遭遇战总预算额度已全部消耗！等待玩家消灭场上剩余怪物。");
        }

        private void OnEncounterClearedAndCompleted()
        {
            Debug.Log(LogPrefix + "恭喜！场上所有怪物被完全肃清，遭遇战胜利结算！");
            _activeEncounterModule = null;
        }

        #endregion
    }
}
