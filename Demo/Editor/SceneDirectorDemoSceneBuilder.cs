using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;
using Cwcbb.Tools.CwcSceneDirector.Demo;

namespace Cwcbb.Tools.CwcSceneDirector.Demo.Editor
{
    /// <summary>
    /// 一键自动化构建 Scene Director 演示场景与配套资产的编辑器工具
    /// 全部资源与代码均完全封闭在 CwcPlugins/CwcSceneDirector/Demo 目录内，独立自洽，零外部业务依赖
    /// </summary>
    public static class SceneDirectorDemoSceneBuilder
    {
        #region 常量与静态 (Constants & Static)

        private const string MenuPath = "Tools/Cwcbb/Scene Director/Build Demo Scene and Assets";
        private const string DemoRootPath = "Assets/CwcPlugins/CwcSceneDirector/Demo";
        private const string GeneratedAssetsPath = "Assets/CwcPlugins/CwcSceneDirector/Demo/GeneratedAssets";
        private const string SceneSavePath = "Assets/CwcPlugins/CwcSceneDirector/Demo/Demo_SceneDirector.unity";

        #endregion

        #region 公开菜单入口 (Public Methods)

        [MenuItem(MenuPath, false, 100)]
        public static void BuildDemoSceneAndAssets()
        {
            EnsureDirectories();

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            int chestTargetLayer = interactableLayer != -1 ? interactableLayer : (obstacleLayer != -1 ? obstacleLayer : 0);

            // 1. 生成测试预制件 (宝箱为中立交互物，放入独立交互物层；杂兵与大怪为战斗单位，挂 Hook 走对象池)
            GameObject chestPrefab = CreateOrUpdatePrefab("Demo_Chest_Prefab", PrimitiveType.Cube, new Vector3(0.9f, 0.9f, 0.9f), Color.yellow, 0, isInteractable: true, targetLayer: chestTargetLayer);
            GameObject minionPrefab = CreateOrUpdatePrefab("Demo_Minion_Prefab", PrimitiveType.Sphere, new Vector3(0.9f, 0.9f, 0.9f), Color.red, 1, isInteractable: false);
            GameObject brutePrefab = CreateOrUpdatePrefab("Demo_Brute_Prefab", PrimitiveType.Cylinder, new Vector3(1.6f, 2.2f, 1.6f), new Color(0.7f, 0.2f, 0.9f), 5, isInteractable: false);

            // 2. 生成单位配置资产 (DemoEnemyUnitSO)
            DemoEnemyUnitSO minionUnitSO = CreateOrUpdateUnitSO("Demo_Unit_Minion", minionPrefab, 1);
            DemoEnemyUnitSO bruteUnitSO = CreateOrUpdateUnitSO("Demo_Unit_Brute", brutePrefab, 5);

            // 3. 生成区域关卡配置资产 (DemoZoneConfigSO)
            DemoZoneConfigSO zoneConfigSO = CreateOrUpdateZoneConfigSO("Demo_ZoneConfig", chestPrefab, minionUnitSO, bruteUnitSO);

            // 4. 构建并保存测试场景
            BuildAndSaveScene(zoneConfigSO);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SceneDirectorDemoSceneBuilder] 演示场景与测试资产创建成功！场景路径：" + SceneSavePath);
        }

        #endregion

        #region 私有资源生成方法 (Private Methods)

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(GeneratedAssetsPath))
            {
                Directory.CreateDirectory(GeneratedAssetsPath);
            }
        }

        private static GameObject CreateOrUpdatePrefab(
            string prefabName,
            PrimitiveType primitiveType,
            Vector3 scale,
            Color color,
            int defaultThreatCost,
            bool isInteractable = false,
            int targetLayer = 0)
        {
            string assetPath = Path.Combine(GeneratedAssetsPath, prefabName + ".prefab").Replace('\\', '/');

            // 若预制体已存在（开发者可能已手动调整过轴心或子层级），直接复用，避免覆盖开发者的定制结构
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existingPrefab != null)
            {
                return existingPrefab;
            }

            // 首次生成：创建标准地面接触点轴心结构（Root 在地面原点，Child 子物体承载模型与碰撞体）
            GameObject rootGo = new GameObject(prefabName);
            if (targetLayer != 0) rootGo.layer = targetLayer;

            GameObject visualGo = GameObject.CreatePrimitive(primitiveType);
            visualGo.name = "Visual";
            visualGo.transform.SetParent(rootGo.transform);
            visualGo.transform.localPosition = new Vector3(0f, scale.y * 0.5f, 0f);
            visualGo.transform.localScale = scale;
            if (targetLayer != 0) visualGo.layer = targetLayer;

            // 设置颜色材质
            Material tempMat = CreateOrUpdateMaterial("Mat_" + prefabName, color);
            var renderer = visualGo.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = tempMat;
            }

            // 仅对非中立交互物的战斗实体挂载 Hook（挂载在 Root 根物体上），交互物不挂 Hook
            if (!isInteractable)
            {
                var hook = rootGo.AddComponent<CwcSceneEntityHook>();
                hook.ThreatCost = defaultThreatCost;
            }

            // 保存为预制体并清理临时对象
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, assetPath);
            Object.DestroyImmediate(rootGo);

            return savedPrefab;
        }

        private static DemoEnemyUnitSO CreateOrUpdateUnitSO(string assetName, GameObject prefab, int baseCost)
        {
            string assetPath = Path.Combine(GeneratedAssetsPath, assetName + ".asset").Replace('\\', '/');

            var unitSO = AssetDatabase.LoadAssetAtPath<DemoEnemyUnitSO>(assetPath);
            if (unitSO == null)
            {
                unitSO = ScriptableObject.CreateInstance<DemoEnemyUnitSO>();
                AssetDatabase.CreateAsset(unitSO, assetPath);
            }

            var so = new SerializedObject(unitSO);
            so.FindProperty("_unitName").stringValue = assetName;
            so.FindProperty("_prefab").objectReferenceValue = prefab;
            so.FindProperty("_baseCost").intValue = baseCost;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(unitSO);
            return unitSO;
        }

        private static DemoZoneConfigSO CreateOrUpdateZoneConfigSO(
            string assetName,
            GameObject chestPrefab,
            DemoEnemyUnitSO minionUnitSO,
            DemoEnemyUnitSO bruteUnitSO)
        {
            string assetPath = Path.Combine(GeneratedAssetsPath, assetName + ".asset").Replace('\\', '/');

            var zoneSO = AssetDatabase.LoadAssetAtPath<DemoZoneConfigSO>(assetPath);
            if (zoneSO == null)
            {
                zoneSO = ScriptableObject.CreateInstance<DemoZoneConfigSO>();
                AssetDatabase.CreateAsset(zoneSO, assetPath);
            }

            int groundLayer = LayerMask.NameToLayer("Ground");
            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            int interactableLayer = LayerMask.NameToLayer("Interactable");

            // 1. 全局维护物理环境设置（墙体与障碍分离，彻底杜绝靠墙吸附堆叠到已有交互物上）
            CwcSceneDirectorSettings.GroundLayer = groundLayer != -1 ? (1 << groundLayer) : (1 << 0);
            CwcSceneDirectorSettings.WallLayer = obstacleLayer != -1 ? (1 << obstacleLayer) : 0;

            // 障碍物层合并 Obstacles 与 Interactable（使得怪群和后续生成物避让已有宝箱与立柱）
            LayerMask obsMask = 0;
            if (obstacleLayer != -1) obsMask |= (1 << obstacleLayer);
            if (interactableLayer != -1) obsMask |= (1 << interactableLayer);
            CwcSceneDirectorSettings.ObstacleLayer = obsMask;
            CwcSceneDirectorSettings.UseNavMesh = false; // 免 NavMesh 物理吸附

            // 2. A. 配置交互物加权单次放置清单（宝箱：天然单体，靠墙吸附，自动在 [Interactables] 默认容器下实例化）
            var interactableConfig = zoneSO.InteractableConfig;
            var chestPayload = new GameObjectPayload(
                chestPrefab,
                PlacementMode.WallSnapped
            );
            var chestEntry = new CwcWeightedEntry<GameObjectPayload>(chestPayload, weight: 15);
            interactableConfig.AddEntry(chestEntry);

            // 3. B. 配置敌群遭遇战抽选清单（外壳仅关注权重，战力消耗完全从小队单体 BaseCost 自动汇总）
            var encounterConfig = zoneSO.EncounterConfig;

            // 小队 1（混编精英队）：1 个强力大怪 (Cost 5) + 3 个杂兵 (Cost 1)，小队自描述 TotalCost = 8
            var squad1 = new AdaptiveSquadPayload<DemoEnemyUnitSO>();
            squad1.AddMember(bruteUnitSO, 1);
            squad1.AddMember(minionUnitSO, 3);
            squad1.SpreadRadius = 5f;
            var squadEntry1 = new CwcCreditEntry<AdaptiveSquadPayload<DemoEnemyUnitSO>>(squad1, weight: 15);
            encounterConfig.AddEntry(squadEntry1);

            // 小队 2（纯怪群冲锋队）：4 个杂兵 (Cost 1)，小队自描述 TotalCost = 4
            var squad2 = new AdaptiveSquadPayload<DemoEnemyUnitSO>();
            squad2.AddMember(minionUnitSO, 4);
            squad2.SpreadRadius = 4f;
            var squadEntry2 = new CwcCreditEntry<AdaptiveSquadPayload<DemoEnemyUnitSO>>(squad2, weight: 30);
            encounterConfig.AddEntry(squadEntry2);

            EditorUtility.SetDirty(zoneSO);
            return zoneSO;
        }

        private static void BuildAndSaveScene(DemoZoneConfigSO zoneConfigSO)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. 创建地牢主平行光（柔和昏暗的顶部下射光）
            GameObject lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.75f;
            light.color = new Color(0.85f, 0.88f, 0.95f);
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer == -1) groundLayer = 0;

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            if (obstacleLayer == -1) obstacleLayer = 0;

            // 2. 创建地牢各类材质 (地面、高台、墙壁、石柱)
            Material matFloor = CreateOrUpdateMaterial("Mat_Dungeon_Floor", new Color(0.22f, 0.23f, 0.26f));
            Material matPlatform = CreateOrUpdateMaterial("Mat_Dungeon_Platform", new Color(0.32f, 0.35f, 0.40f));
            Material matWall = CreateOrUpdateMaterial("Mat_Dungeon_Wall", new Color(0.16f, 0.17f, 0.19f));
            Material matPillar = CreateOrUpdateMaterial("Mat_Dungeon_Pillar", new Color(0.26f, 0.27f, 0.30f));

            // 3. 构建多区域地牢地形 (地面 + 墙壁 + 柱子 + 火把光源)
            GameObject envRoot = new GameObject("[Environment]");
            new GameObject("[Interactables]"); // 供宝箱等交互物固定挂载的根容器

            BuildDungeonGeometry(envRoot.transform, groundLayer, obstacleLayer, matFloor, matPlatform, matWall, matPillar);

            // 4. 创建玩家测试对象 (Player)，初始位于南部起点庭院
            GameObject playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player (WASD Move)";
            playerGo.tag = "Player";
            playerGo.layer = 2; // Ignore Raycast（Unity 标准内置图层，防止自身遮挡地面探测与刷怪射线）
            playerGo.transform.position = new Vector3(0f, 1f, -30f);

            // 保持纯粹显示与触发器胶囊体，彻底移除 CharacterController 及其对 Physics 碰撞矩阵的隐式依赖
            var originalCol = playerGo.GetComponent<Collider>();
            if (originalCol != null)
            {
                originalCol.isTrigger = true; // 改为触发器，不参与硬物理模拟
            }

            // 赋予明亮青绿色外观
            var playerRenderer = playerGo.GetComponent<Renderer>();
            if (playerRenderer != null)
            {
                Material playerMat = CreateOrUpdateMaterial("Mat_Demo_Player", new Color(0.2f, 0.95f, 0.4f));
                playerRenderer.sharedMaterial = playerMat;
            }

            // 挂载轻量玩家控制器
            var playerController = playerGo.AddComponent<DemoPlayerController>();

            // 5. 创建主摄像机并挂载平滑跟随组件
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 21f, -44f);
            cameraGo.transform.rotation = Quaternion.Euler(52f, 0f, 0f);

            var cameraFollow = cameraGo.AddComponent<DemoCameraFollow>();
            cameraFollow.SetTarget(playerGo.transform, snapImmediately: true);

            // 6. 创建场景导演与多地形测试器节点
            GameObject triggerGo = new GameObject("[Demo_SceneDirector_Trigger]");
            triggerGo.transform.position = Vector3.zero;

            var sphereCollider = triggerGo.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = 16f;

            var tester = triggerGo.AddComponent<DemoSceneDirectorTester>();
            var testerSO = new SerializedObject(tester);
            testerSO.FindProperty("_zoneConfig").objectReferenceValue = zoneConfigSO;
            testerSO.FindProperty("_usePlayerPositionAsCenter").boolValue = true; // 默认跟随玩家当前所在位置与房间动态测试
            testerSO.FindProperty("_spawnInteractablesOnStart").boolValue = true;  // 开局自动生成交互物宝箱
            testerSO.FindProperty("_startEncounterOnStart").boolValue = false;     // 开局不盲目刷怪，由玩家移动到各区域或一键传送后按 [E] 触发
            testerSO.FindProperty("_interactableUseMapCenter").boolValue = true;   // 宝箱默认采用全图几何中心覆盖全图各大房间
            testerSO.FindProperty("_interactableRadius").floatValue = 55f;         // 全图辐射半径 55m
            testerSO.FindProperty("_interactableMaxClusters").intValue = 36;       // 全图上限 36 个宝箱群落
            testerSO.FindProperty("_interactableMinDistance").floatValue = 7.5f;   // 泊松盘排斥间距 7.5m
            testerSO.FindProperty("_useSpatialGrid").boolValue = true;             // 默认启用场景空间拓扑感知与最远点均匀采样（FPS）
            testerSO.FindProperty("_clearChestsBeforeRespawn").boolValue = true;   // 重新生成前清理旧宝箱
            testerSO.FindProperty("_minEncounterRadius").floatValue = 8f;
            testerSO.FindProperty("_encounterRadius").floatValue = 22f;
            testerSO.ApplyModifiedProperties();

            // 保存场景文件
            EditorSceneManager.SaveScene(scene, SceneSavePath);
        }

        private static void BuildDungeonGeometry(
            Transform parent,
            int groundLayer,
            int obstacleLayer,
            Material matFloor,
            Material matPlatform,
            Material matWall,
            Material matPillar)
        {
            Transform floorsRoot = new GameObject("Floors").transform;
            floorsRoot.SetParent(parent);

            Transform wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(parent);

            Transform obstaclesRoot = new GameObject("Obstacles").transform;
            obstaclesRoot.SetParent(parent);

            Transform lightsRoot = new GameObject("Torches").transform;
            lightsRoot.SetParent(parent);

            // ==========================================
            // 区域 1：南部起点庭院 (South Entry Courtyard)
            // ==========================================
            CreateBox(floorsRoot, "Floor_SouthEntry", new Vector3(0f, -0.5f, -30f), new Vector3(20f, 1f, 16f), groundLayer, matFloor);
            CreateBox(wallsRoot, "Wall_SouthEntry_S", new Vector3(0f, 2f, -38.5f), new Vector3(21f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_SouthEntry_W", new Vector3(-10.5f, 2f, -30f), new Vector3(1f, 4f, 16f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_SouthEntry_E", new Vector3(10.5f, 2f, -30f), new Vector3(1f, 4f, 16f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_SouthEntry_NW", new Vector3(-7.5f, 2f, -22f), new Vector3(7f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_SouthEntry_NE", new Vector3(7.5f, 2f, -22f), new Vector3(7f, 4f, 1f), obstacleLayer, matWall);
            CreatePointLight(lightsRoot, "Torch_SouthEntry", new Vector3(0f, 3.5f, -30f), new Color(1f, 0.85f, 0.65f), 18f, 1.4f);

            // 南连廊 (South Corridor)
            CreateBox(floorsRoot, "Floor_SouthCorridor", new Vector3(0f, -0.5f, -17f), new Vector3(8f, 1f, 10f), groundLayer, matFloor);
            CreateBox(wallsRoot, "Wall_SouthCorridor_W", new Vector3(-4.5f, 2f, -17f), new Vector3(1f, 4f, 10f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_SouthCorridor_E", new Vector3(4.5f, 2f, -17f), new Vector3(1f, 4f, 10f), obstacleLayer, matWall);

            // ==========================================
            // 区域 2：中央大殿 (Central Hall - 开阔竞技场)
            // ==========================================
            CreateBox(floorsRoot, "Floor_CentralHall", new Vector3(0f, -0.5f, 0f), new Vector3(24f, 1f, 24f), groundLayer, matFloor);
            CreateBox(wallsRoot, "Wall_Central_SW", new Vector3(-8.5f, 2f, -12.5f), new Vector3(9f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Central_SE", new Vector3(8.5f, 2f, -12.5f), new Vector3(9f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Central_NW", new Vector3(-8f, 2f, 12.5f), new Vector3(10f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Central_NE", new Vector3(8f, 2f, 12.5f), new Vector3(10f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Central_WN", new Vector3(-12.5f, 2f, 7.5f), new Vector3(1f, 4f, 9f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Central_WS", new Vector3(-12.5f, 2f, -7.5f), new Vector3(1f, 4f, 9f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Central_EN", new Vector3(12.5f, 2f, 8f), new Vector3(1f, 4f, 8f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Central_ES", new Vector3(12.5f, 2f, -8f), new Vector3(1f, 4f, 8f), obstacleLayer, matWall);

            // 中央大殿 4 根石柱
            CreateBox(obstaclesRoot, "Pillar_Hall_1", new Vector3(-6f, 2f, -6f), new Vector3(1.6f, 4f, 1.6f), obstacleLayer, matPillar);
            CreateBox(obstaclesRoot, "Pillar_Hall_2", new Vector3(6f, 2f, -6f), new Vector3(1.6f, 4f, 1.6f), obstacleLayer, matPillar);
            CreateBox(obstaclesRoot, "Pillar_Hall_3", new Vector3(-6f, 2f, 6f), new Vector3(1.6f, 4f, 1.6f), obstacleLayer, matPillar);
            CreateBox(obstaclesRoot, "Pillar_Hall_4", new Vector3(6f, 2f, 6f), new Vector3(1.6f, 4f, 1.6f), obstacleLayer, matPillar);
            CreatePointLight(lightsRoot, "Torch_CentralHall", new Vector3(0f, 4.5f, 0f), new Color(1f, 0.72f, 0.38f), 26f, 2.0f);

            // ==========================================
            // 区域 3：北部狭窄走廊 (North Corridor - 宽6米，长24米通道)
            // ==========================================
            CreateBox(floorsRoot, "Floor_NorthCorridor", new Vector3(0f, -0.5f, 24f), new Vector3(6f, 1f, 24f), groundLayer, matFloor);
            CreateBox(wallsRoot, "Wall_NorthCorridor_W", new Vector3(-3.5f, 2f, 24f), new Vector3(1f, 4f, 24f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_NorthCorridor_E", new Vector3(3.5f, 2f, 24f), new Vector3(1f, 4f, 24f), obstacleLayer, matWall);
            CreatePointLight(lightsRoot, "Torch_NorthCorridor_1", new Vector3(0f, 3.5f, 18f), new Color(0.85f, 0.75f, 1f), 12f, 1.0f);
            CreatePointLight(lightsRoot, "Torch_NorthCorridor_2", new Vector3(0f, 3.5f, 30f), new Color(0.85f, 0.75f, 1f), 12f, 1.0f);

            // ==========================================
            // 区域 4：北部高台圣殿 (North Sanctum - 高度差、台阶与高台测试)
            // ==========================================
            CreateBox(floorsRoot, "Floor_NorthSanctum", new Vector3(0f, -0.5f, 48f), new Vector3(24f, 1f, 24f), groundLayer, matFloor);
            CreateBox(wallsRoot, "Wall_Sanctum_N", new Vector3(0f, 2f, 60.5f), new Vector3(25f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Sanctum_W", new Vector3(-12.5f, 2f, 48f), new Vector3(1f, 4f, 24f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Sanctum_E", new Vector3(12.5f, 2f, 48f), new Vector3(1f, 4f, 24f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Sanctum_SW", new Vector3(-8f, 2f, 35.5f), new Vector3(10f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Sanctum_SE", new Vector3(8f, 2f, 35.5f), new Vector3(10f, 4f, 1f), obstacleLayer, matWall);

            // 中央高台与过渡台阶 (GroundLayer，顶部平面的 Y 分别为 0.5f 和 1.0f)
            CreateBox(floorsRoot, "Step_Sanctum_Lower", new Vector3(0f, 0.0f, 42.5f), new Vector3(8f, 0.5f, 3f), groundLayer, matPlatform);
            CreateBox(floorsRoot, "Platform_Sanctum_Main", new Vector3(0f, 0.5f, 50f), new Vector3(12f, 1f, 12f), groundLayer, matPlatform);

            // 高台两侧祭坛立柱
            CreateBox(obstaclesRoot, "Pillar_Sanctum_L", new Vector3(-4f, 3f, 52f), new Vector3(1.4f, 4f, 1.4f), obstacleLayer, matPillar);
            CreateBox(obstaclesRoot, "Pillar_Sanctum_R", new Vector3(4f, 3f, 52f), new Vector3(1.4f, 4f, 1.4f), obstacleLayer, matPillar);
            CreatePointLight(lightsRoot, "Torch_NorthSanctum", new Vector3(0f, 4.5f, 50f), new Color(0.35f, 0.75f, 1f), 22f, 2.2f);

            // ==========================================
            // 区域 5：西部藏宝室 (West Crypt - 凹槽壁龛与宝箱靠墙吸附测试)
            // ==========================================
            // 西走廊
            CreateBox(floorsRoot, "Floor_WestCorridor", new Vector3(-18f, -0.5f, 0f), new Vector3(12f, 1f, 6f), groundLayer, matFloor);
            CreateBox(wallsRoot, "Wall_WestCorridor_N", new Vector3(-18f, 2f, 3.5f), new Vector3(12f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_WestCorridor_S", new Vector3(-18f, 2f, -3.5f), new Vector3(12f, 4f, 1f), obstacleLayer, matWall);

            // 藏宝室
            CreateBox(floorsRoot, "Floor_WestCrypt", new Vector3(-33f, -0.5f, 0f), new Vector3(18f, 1f, 18f), groundLayer, matFloor);
            CreateBox(wallsRoot, "Wall_Crypt_W", new Vector3(-42.5f, 2f, 0f), new Vector3(1f, 4f, 19f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Crypt_N", new Vector3(-33f, 2f, 9.5f), new Vector3(19f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Crypt_S", new Vector3(-33f, 2f, -9.5f), new Vector3(19f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Crypt_EN", new Vector3(-23.5f, 2f, 6.25f), new Vector3(1f, 4f, 6.5f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_Crypt_ES", new Vector3(-23.5f, 2f, -6.25f), new Vector3(1f, 4f, 6.5f), obstacleLayer, matWall);

            // 室内凹槽壁龛隔断墙（构造靠墙死角）
            CreateBox(obstaclesRoot, "Alcove_Crypt_N", new Vector3(-33f, 2f, 6.5f), new Vector3(6f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(obstaclesRoot, "Alcove_Crypt_S", new Vector3(-33f, 2f, -6.5f), new Vector3(6f, 4f, 1f), obstacleLayer, matWall);
            CreatePointLight(lightsRoot, "Torch_WestCrypt", new Vector3(-33f, 4f, 0f), new Color(1f, 0.85f, 0.25f), 18f, 1.8f);

            // ==========================================
            // 区域 6：东部柱林大厅 (East Pillar Hall - 密集障碍物排斥测试)
            // ==========================================
            // 东走廊
            CreateBox(floorsRoot, "Floor_EastCorridor", new Vector3(18f, -0.5f, 0f), new Vector3(12f, 1f, 8f), groundLayer, matFloor);
            CreateBox(wallsRoot, "Wall_EastCorridor_N", new Vector3(18f, 2f, 4.5f), new Vector3(12f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_EastCorridor_S", new Vector3(18f, 2f, -4.5f), new Vector3(12f, 4f, 1f), obstacleLayer, matWall);

            // 柱林大厅
            CreateBox(floorsRoot, "Floor_EastHall", new Vector3(34f, -0.5f, 0f), new Vector3(20f, 1f, 20f), groundLayer, matFloor);
            CreateBox(wallsRoot, "Wall_EastHall_E", new Vector3(44.5f, 2f, 0f), new Vector3(1f, 4f, 21f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_EastHall_N", new Vector3(34f, 2f, 10.5f), new Vector3(21f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_EastHall_S", new Vector3(34f, 2f, -10.5f), new Vector3(21f, 4f, 1f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_EastHall_WN", new Vector3(23.5f, 2f, 7.25f), new Vector3(1f, 4f, 6.5f), obstacleLayer, matWall);
            CreateBox(wallsRoot, "Wall_EastHall_WS", new Vector3(23.5f, 2f, -7.25f), new Vector3(1f, 4f, 6.5f), obstacleLayer, matWall);

            // 6 根密集石柱矩阵
            CreateBox(obstaclesRoot, "Pillar_Dense_1", new Vector3(29f, 2f, 4.5f), new Vector3(1.5f, 4f, 1.5f), obstacleLayer, matPillar);
            CreateBox(obstaclesRoot, "Pillar_Dense_2", new Vector3(34f, 2f, 4.5f), new Vector3(1.5f, 4f, 1.5f), obstacleLayer, matPillar);
            CreateBox(obstaclesRoot, "Pillar_Dense_3", new Vector3(39f, 2f, 4.5f), new Vector3(1.5f, 4f, 1.5f), obstacleLayer, matPillar);
            CreateBox(obstaclesRoot, "Pillar_Dense_4", new Vector3(29f, 2f, -4.5f), new Vector3(1.5f, 4f, 1.5f), obstacleLayer, matPillar);
            CreateBox(obstaclesRoot, "Pillar_Dense_5", new Vector3(34f, 2f, -4.5f), new Vector3(1.5f, 4f, 1.5f), obstacleLayer, matPillar);
            CreateBox(obstaclesRoot, "Pillar_Dense_6", new Vector3(39f, 2f, -4.5f), new Vector3(1.5f, 4f, 1.5f), obstacleLayer, matPillar);
            CreatePointLight(lightsRoot, "Torch_EastHall", new Vector3(34f, 4f, 0f), new Color(1f, 0.55f, 0.2f), 20f, 2.0f);
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, int layer, Material mat)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.layer = layer;
            box.transform.SetParent(parent);
            box.transform.position = position;
            box.transform.localScale = scale;

            if (mat != null)
            {
                var renderer = box.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = mat;
                }
            }

            return box;
        }

        private static GameObject CreatePointLight(Transform parent, string name, Vector3 position, Color color, float range, float intensity)
        {
            GameObject lightGo = new GameObject(name);
            lightGo.transform.SetParent(parent);
            lightGo.transform.position = position;

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;

            return lightGo;
        }

        private static Material CreateOrUpdateMaterial(string matName, Color color)
        {
            string matPath = Path.Combine(GeneratedAssetsPath, matName + ".mat").Replace('\\', '/');
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
            {
                existingMat.color = color;
                if (existingMat.HasProperty("_BaseColor"))
                {
                    existingMat.SetColor("_BaseColor", color);
                }
                EditorUtility.SetDirty(existingMat);
                return existingMat;
            }

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        #endregion
    }
}
