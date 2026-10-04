# CwcSceneDirector - Scene Content & AI Spawning Director System

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Custom%20(Free%20for%20Games)-blue.svg)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/CwcbbChao/CwcSceneDirector/pulls)

**English** | [简体中文](README.md)

---

## Overview

`CwcSceneDirector` is a **high-performance, modular, pure C#-driven** scene content placement and dynamic AI spawning director system designed for Unity.

The system combines the macro candidate sampling and quota-deck shuffling algorithms of **Diablo IV** with the credit-budget and wave-pacing mechanisms of **Risk of Rain 2**. It unifies macro candidate sampling, micro tactical formations, physical ground validation, entity pooling, and distance-based culling into a clean, decoupled execution pipeline. Ideal for Roguelite dungeons, open-world combat encounters, chest placements, and world events.

The core framework is built entirely on pure C# classes and interfaces, has **zero external project dependencies**, supports on-demand lazy creation, and runs with zero garbage collection allocations.

---

## Core Features

### 1. Diablo IV Two-Level Placement & Quota Deck
- **Two-Level Separation**: Decouples macro cluster candidate selection from micro squad dispersal based on tactical formations and physical bounding footprints.
- **Quota Deck Algorithm**:
  - **Phase 1 (Guaranteed Limits)**: Satisfies mandatory `MinLimit` quotas first;
  - **Phase 2 (Proportional Weights)**: Distributes remaining quotas proportionally by `Weight` to items that haven't hit their `MaxLimit`;
  - **Phase 3 (Shuffling)**: Shuffles the deck using Fisher-Yates randomization. Ensures controlled overall output ratios while maintaining natural unpredictability.

### 2. Risk of Rain 2-Inspired Credit Director
- **Target-Lock Anti-Starvation**: When a high-cost elite is rolled but concurrent capacity is full, the director locks the target and waits for players to clear enemies, preventing high-cost monsters from starving due to narrow spawn windows.
- **Soft-Cap Overdraft**: Allows purchasing an entire squad and overdrawing the budget into negative values when remaining budget is greater than zero, avoiding deadlocks with leftover fractional credits.
- **Unidirectional Budget Audit**: Credits are not deducted upfront. The entity manager deducts budget based on the actual `ThreatCost` when an entity activates from the pool, ensuring exact accounting.
- **Wave Breathing Cooldown**: Introduces physiological cooldown timers after each wave, working alongside `MaxConcurrentCost` to produce well-paced combat flow.

### 3. Spacious Farthest Point Sampling (Spacious-FPS)
- Combines **local ground point cloud fullness (representing room area)** with **Farthest Point Sampling (FPS)** to prioritize placing cluster centers in large room clearings.
- Spreads spawns evenly across rooms while preventing clusters in narrow hallways, using pure point-cloud topology analysis without physics raycast overhead.

### 4. Sunflower / Vogel Golden Spiral Formations
- Micro squad dispersion utilizes the $137.5^\circ$ golden angle sunflower spiral (Vogel Spiral). Large monsters stay centered ($r = 0$), while minions spiral outward with uniform areal density.
- **Inward Bounce**: When outer points strike walls or ledges, they automatically pull inward toward the cluster center.
- **Fallback Recovery ("No Monster Left Behind")**: If extreme terrain blocks a spot, replacement points are sampled within the spread radius to ensure full squad counts are spawned without losing units.

### 5. Physical Footprint Auto-Detection & Ledge Safety Probing
- **Automatic Footprint Detection**: Computes clearance radius $R$ directly from native `Collider`, `CharacterController`, or `Renderer` bounds without requiring marker interfaces or custom components on prefabs.
- **Ledge Safety Probes (`IsLedgeSafe`)**: Employs four downward probes offset by radius $R$. Any point hanging over voids or exceeding step thresholds is rejected, eliminating floating or cliff-edge spawns.
- **Wall Snapping (`TrySnapToWall`)**: Pushes objects back by radius $R$ for flush alignment against walls, automatically facing toward open interior space.

### 6. High-Performance 2D Spatial Hash Grid & 2.5D World Chunks
- **2D Spatial Hash Grid (`CwcSpatialGrid`)**: Partitions entities on the X-Z plane with pooled lists, providing near $O(1)$ zero-allocation queries for nearby units and threat costs.
- **2.5D World Chunks (`CwcSpatialChunk`)**: Lazy-scans walkable terrain and supports localized invalidation for dynamic obstacles and environment destruction.

### 7. Time-Sliced Frame Budgeting
- The runtime context (`CwcPlacementContext`) includes a high-precision `Stopwatch`. It yields control (`yield return null`) only when the frame budget (default 2.0ms) is exceeded, achieving rapid instantiation without dropping frames.

### 8. Centralized Entity Pooling & Distance-Based Culling
- Entities outside the focus target's culling radius trigger automatic `Despawn()` back into the pool. Features spawn grace periods (`_cullGracePeriod`) and boss exemptions (`AllowCulling = false`).
- Neatly organizes the scene hierarchy by prefab categories.

### 9. Modern Card PropertyDrawer & Runtime Monitor Inspector
- Compact custom card drawer featuring type-based accent colors (cyan for interactables, orange for credit squads, purple for generic items), inline weight inputs, foldouts, and script jump navigation.
- Live editor inspector displays module status (Running / Paused / Completed) and execution order with manual debug controls.

---

## Installation

### Option A: Via Unity Package Manager (Git URL Recommended)
1. In the Unity Editor, open `Window` -> `Package Manager`.
2. Click the `+` button in the upper-left corner -> Select **Add package from git URL...**.
3. Enter the repository URL:
   ```text
   https://github.com/CwcbbChao/CwcSceneDirector.git
   ```
4. Click **Add** to install.

### Option B: Direct Source Import
Clone or copy the `CwcSceneDirector` directory directly into your project's `Assets/` or `Packages/` folder.

---

## Quick Start

### 1. Static Interactable / Chest Weighted Placement
```csharp
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

public class DungeonChestSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _woodenChestPrefab;
    [SerializeField] private GameObject _goldChestPrefab;
    [SerializeField] private GameObject _shrinePrefab;

    private void Start()
    {
        // Create placement module: 40m radius from current position, up to 12 clusters, 8m minimum distance
        var placementModule = new CwcWeightedPlacementModule(
            center: transform.position,
            radius: 40f,
            minClusterDistance: 8f,
            maxClusters: 12
        );

        // Standard chest: Weight 70, wall-snapped, 1 per cluster
        placementModule.AddItem(new CwcInteractablePlacementItem(_woodenChestPrefab, weight: 70, mode: PlacementMode.WallSnapped));

        // Gold chest: Weight 20, wall-snapped, 1 per cluster, maximum 2
        var goldChestItem = new CwcInteractablePlacementItem(_goldChestPrefab, weight: 20, mode: PlacementMode.WallSnapped);
        goldChestItem.MaxLimit = 2;
        placementModule.AddItem(goldChestItem);

        // Shrine: Weight 10, open ground, guaranteed at least 1
        var shrineItem = new CwcInteractablePlacementItem(_shrinePrefab, weight: 10, mode: PlacementMode.OpenCenter);
        shrineItem.MinLimit = 1;
        placementModule.AddItem(shrineItem);

        // Register to the scene director (automatically cleans up upon completion)
        CwcSceneDirector.Register(placementModule);
    }
}
```

### 2. Dynamic Monster Wave / Encounter Spawner
```csharp
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

public class ArenaEncounterSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _minionPrefab;
    [SerializeField] private GameObject _elitePrefab;
    [SerializeField] private Transform _playerTransform;

    private CwcCreditDirectorModule _encounterModule;

    public void StartEncounter()
    {
        // Create credit director: Follows player, 30m outer radius, 8m inner safe radius, 60 total budget, 15 concurrent cap
        _encounterModule = new CwcCreditDirectorModule(
            center: _playerTransform.position,
            radius: 30f,
            totalBudget: 60,
            maxConcurrentCost: 15,
            minWaveCost: 2,
            minRadius: 8f,
            centerTarget: _playerTransform
        );

        // Set wave breathing cooldown to 3.0 seconds
        _encounterModule.SetWaveCooldown(3.0f);

        // Minions: 1 cost each, weight 80, 4 per squad (squad cost 4)
        _encounterModule.AddItem(new CwcCreditPrefabPlacementItem(_minionPrefab, cost: 4, weight: 80, count: 4));

        // Elite: 5 cost each, weight 20, 1 per squad (squad cost 5), spawns in open ground
        _encounterModule.AddItem(new CwcCreditPrefabPlacementItem(_elitePrefab, cost: 5, weight: 20, count: 1, mode: PlacementMode.OpenCenter));

        // Event callbacks for budget depletion and completion
        _encounterModule.OnBudgetDepleted += () => Debug.Log("Warning: Enemy reinforcements have been exhausted!");
        _encounterModule.OnClearedAndCompleted += () => Debug.Log("Victory! All enemies defeated!");

        // Register and activate director
        CwcSceneDirector.Register(_encounterModule);
    }

    public void StopEncounter()
    {
        if (_encounterModule != null)
        {
            CwcSceneDirector.Unregister(_encounterModule);
            _encounterModule = null;
        }
    }
}
```

---

## Demo Showcase

- **Scene Path**: `Assets/CwcPlugins/CwcSceneDirector/Demo/Demo_SceneDirector.unity`.
- **Out of the Box**: Ready to play immediately after importing the package.
- **Keyboard Controls**:
  - **I**: Spawns uniformly distributed chests and interactables across the six dungeon chambers;
  - **C**: Clears all spawned chests in the scene;
  - **E**: Activates the dynamic monster encounter wave;
  - **K**: Stops the active encounter;
  - **X**: Simulates clearing all active enemies on screen.
- **Fully Decoupled**: The Demo module uses its own assembly definitions (`.asmdef`). Core `Runtime` and `Editor` modules have zero reverse dependencies on the demo.

---

## Core Architecture & Responsibilities

| Class | Role | Description |
| :--- | :--- | :--- |
| `CwcSceneDirector` | Core Scheduling Hub | MonoBehaviour-driven manager supporting lazy creation, priority sorting, and pause/resume |
| `CwcSceneDirectorModule` | Abstract Module Base | Pure C# logic container with full lifecycle hooks and coroutine support |
| `CwcWeightedPlacementModule` | One-Shot Placement Module | Diablo IV quota deck shuffling with guarantees, proportional weights, and auto-cleanup |
| `CwcCreditDirectorModule` | Dynamic Credit Director | Risk of Rain 2 encounter director with target lock, overdraft, and wave cooldown |
| `CwcSceneEntityManager` | Entity Pool & Cull Manager | Centralized pool hierarchy, distance-based culling, focus tracking, and threat cost aggregation |
| `CwcSpatialGrid` | 2D Spatial Hash Grid | Horizontal 2D hash grid providing near $O(1)$ zero-allocation entity queries |
| `CwcSceneSpatialManager` | 2.5D Chunked Point Cloud | World-chunk terrain scanner providing Spacious-FPS and localized invalidation |
| `CwcPlacementSpatialUtil` | Spatial Geometry Utilities | Algorithm library for Poisson sampling, Vogel spiral, footprint detection, and ledge probing |
| `IPlacementStrategy` | Polymorphic Placement Policy | Strategy pattern contract with `Free`, `WallSnapped`, and `OpenCenter` implementations |
| `CwcPlacementContext` | Runtime Context | Carries scheduling hosts, physics settings, and high-precision frame-budget timers |

---

## License & Commercial Use

- The core source code is licensed under the [Cwc Tools Public License (Source-Available)](LICENSE):
  - **Game Projects (End Products)**: Free to integrate, customize, and commercially distribute in personal and commercial games with zero royalties (Royalty-Free).
  - **Redistribution Restrictions (No Redistribution as Tools)**: You may not redistribute, resell, or host this software (modified or unmodified) as a standalone development tool, asset pack, or competing library.
- See [Third-Party Notices.txt](Third-Party%20Notices.txt) for asset licensing details.
