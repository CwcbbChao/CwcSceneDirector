# CwcSceneDirector - Scene Placement & Dynamic AI Spawning Director

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Custom%20(Free%20for%20Games)-blue.svg)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/CwcbbChao/CwcSceneDirector/pulls)

**English** | [简体中文](README.md)

---

## What Does It Do?

When making dungeons, Roguelites, open-world games, or combat encounters, developers frequently need to:
1. **Place things in the scene**: Scatter chests, shrines, resource nodes, or traps with **even distribution, controlled ratios, proper wall alignment, and never floating over cliffs**.
2. **Spawn enemies during combat**: Like in *Risk of Rain 2* or *Diablo IV*, dynamically spawn minions and elites with **squad formations, pacing that slows when players struggle and speeds up when they clear fast, and zero frame drops**.

`CwcSceneDirector` is an open-source Unity package designed specifically for these workflows. It is **pure C#-driven, lightweight, high-performance, and works out of the box**.

---

## Key Problems It Solves

### 1. Chests & Props: Controlled Ratios, Never Clumping
- **Shuffled Quota Deck**: Configure item ratios such as 70% wooden chests, 20% gold chests, and 10% shrines, with strict rules like "at least 1 shrine guaranteed, at most 2 gold chests total". The system allocates the items upfront and shuffles them like a deck of cards, eliminating bad RNG where only gold chests spawn or no shrines appear.
- **Smart Alignment & Wall Snapping**:
  - **Free**: Places objects flush on the ground with mutual spacing to prevent overlaps.
  - **WallSnapped**: Finds the nearest wall, snaps flush against it, and **automatically turns the object to face inward toward the room**.
  - **OpenCenter**: Finds the most open, flat area in the center of a room.

### 2. Dynamic Spawning & Encounters: Pacing Over Brainless Swarms
- **Credit Budget & Concurrency Caps**: Give the director a total budget (e.g., 60 total threat points for an encounter) and an on-screen cap (e.g., maximum 15 points active at once).
- **Pacing with Breathing Room**: When the player is overwhelmed, the director pauses spawns until enemies are cleared. A configurable wave cooldown prevents monsters from constantly trickling in.
- **Anti-Starvation for Elites**: If a high-cost boss or elite squad is rolled but on-screen capacity is full, the director **locks and queues the elite** until the player clears enough minions to free up space. Expensive enemies never get skipped by cheap ones.
- **Budget Overdraft**: When nearing the end of an encounter with only 2 points left, if a 5-point squad is rolled, the director allows "overdrawing" the budget to spawn the full squad, avoiding deadlocks with leftover fractional points.
- **No Face-Spawning**: A safe inner radius ensures enemies never spawn directly on top of the player.

### 3. Natural Formations: Elites Centered, Minions Spreading
- **Sunflower Vogel Spiral**: When a squad spawns, the elite stands firmly in the center while minions circle outward like sunflower petals with uniform density, avoiding single-point stacking.
- **Inward Wall Bouncing**: If the outer edge of a formation hits a wall or obstacle, minions automatically bounce inward toward the center.
- **Zero-Drop Fallback**: If extreme terrain blocks a spot, replacement points are automatically sampled nearby to guarantee that all configured units are spawned.

### 4. Footprint Auto-Detection & Ledge Probes: Grounded Every Time
- **Zero Extra Scripts on Prefabs**: Automatically calculates footprint radius from native `Collider`, `CharacterController`, or `Renderer` bounds.
- **Four-Way Ledge Checks**: Before placing an object, downward probes check the perimeter. If any point hangs over a void or drops off a steep cliff, the spot is rejected, eliminating monsters stuck mid-air over chasms.

### 5. Time-Sliced Frame Budget: Smooth Spawning Without Lag
- Traditional `Instantiate` calls in a single frame cause noticeable stutter.
- Built-in time-slicing (default 2.0ms per frame) batches spawns when the frame is smooth and yields (`yield return null`) to the next frame when time runs low, maintaining rock-solid frame rates.

### 6. Built-in Pooling & Distance Culling
- When enemies move too far from the player (e.g., beyond 60m), they automatically return to the pool without requiring manual pooling code.
- Includes spawn grace periods and boss exemption flags to prevent accidental despawns.

---

## Installation

### Option A: Via Unity Package Manager (Recommended)
1. In the Unity Editor, open `Window` -> `Package Manager`.
2. Click the `+` button in the top-left corner -> Select **Add package from git URL...**.
3. Enter the repository URL:
   ```text
   https://github.com/CwcbbChao/CwcSceneDirector.git
   ```
4. Click **Add** to install.

### Option B: Copy Files Directly
Clone or copy the `CwcSceneDirector` directory directly into your project's `Assets/` or `Packages/` folder.

---

## Quick Start

### Scenario 1: Placing Chests and Shrines in a Dungeon

```csharp
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

public class ChestSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _woodenChestPrefab;
    [SerializeField] private GameObject _goldChestPrefab;
    [SerializeField] private GameObject _shrinePrefab;

    private void Start()
    {
        // 1. Create module: 35m radius, up to 10 clusters, at least 7m apart
        var placement = new CwcWeightedPlacementModule(
            center: transform.position,
            radius: 35f,
            minClusterDistance: 7f,
            maxClusters: 10
        );

        // 2. Wooden chests: Weight 70, snap to walls
        placement.AddItem(new CwcInteractablePlacementItem(_woodenChestPrefab, weight: 70, mode: PlacementMode.WallSnapped));

        // 3. Gold chests: Weight 20, snap to walls, capped at 2 maximum
        var goldChest = new CwcInteractablePlacementItem(_goldChestPrefab, weight: 20, mode: PlacementMode.WallSnapped);
        goldChest.MaxLimit = 2;
        placement.AddItem(goldChest);

        // 4. Buff shrines: Weight 10, open clearing, guaranteed at least 1
        var shrine = new CwcInteractablePlacementItem(_shrinePrefab, weight: 10, mode: PlacementMode.OpenCenter);
        shrine.MinLimit = 1;
        placement.AddItem(shrine);

        // 5. Register to the director (cleans up automatically when done)
        CwcSceneDirector.Register(placement);
    }
}
```

---

### Scenario 2: Risk of Rain-Style Combat Encounter Wave

```csharp
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

public class MonsterEncounter : MonoBehaviour
{
    [SerializeField] private GameObject _minionPrefab;
    [SerializeField] private GameObject _bossPrefab;
    [SerializeField] private Transform _player;

    private CwcCreditDirectorModule _director;

    public void StartWave()
    {
        // 1. Create director: Follows player, 28m outer radius, 7m safe inner radius, 50 total budget, 14 concurrent cap
        _director = new CwcCreditDirectorModule(
            center: _player.position,
            radius: 28f,
            totalBudget: 50,
            maxConcurrentCost: 14,
            minWaveCost: 2,
            minRadius: 7f,
            centerTarget: _player
        );

        // 2. Wave breathing cooldown: 3.5s pause between waves
        _director.SetWaveCooldown(3.5f);

        // 3. Squad rosters:
        // Minion squad: 3 minions per squad, costs 3 points, weight 80
        _director.AddItem(new CwcCreditPrefabPlacementItem(_minionPrefab, cost: 3, weight: 80, count: 3));

        // Elite: Costs 6 points, weight 20, 1 per squad, spawns in open areas
        _director.AddItem(new CwcCreditPrefabPlacementItem(_bossPrefab, cost: 6, weight: 20, count: 1, mode: PlacementMode.OpenCenter));

        // 4. Event listeners
        _director.OnBudgetDepleted += () => Debug.Log("Reinforcements depleted!");
        _director.OnClearedAndCompleted += () => Debug.Log("All enemies cleared, victory!");

        // 5. Start the director
        CwcSceneDirector.Register(_director);
    }

    public void StopWave()
    {
        if (_director != null)
        {
            CwcSceneDirector.Unregister(_director);
            _director = null;
        }
    }
}
```

---

## Configuration Reference

| Parameter | Meaning | Recommendation |
| :--- | :--- | :--- |
| `PlacementMode.Free` | **Free ground**: Places flush on terrain with clearance checks | Common enemies, rocks, wildlife |
| `PlacementMode.WallSnapped` | **Wall snapped**: Snaps flush against walls, facing room interior | Chests, bookshelves, torches, doors |
| `PlacementMode.OpenCenter` | **Open clearing**: Avoids walls and narrow corridors, centers in open areas | Large bosses, altars, focal shrines |
| `MinLimit / MaxLimit` | **Guaranteed min / hard ceiling**: Overrides random weights | Guarantee at least 1 shrine, cap gold chests at 2 |
| `totalBudget` | **Total budget**: Total points for the encounter | Small fight: 30~50, large swarm: 100~200 |
| `maxConcurrentCost` | **Concurrent cap**: Director pauses when active units hit this value | Prevents overwhelming the player |
| `minRadius` | **Inner safe radius**: Minimum spawn distance from target | 6m~10m to prevent instant face-spawns |
| `SetWaveCooldown` | **Wave cooldown**: Breathing room between spawn batches | 2.5s~4.0s for good combat pacing |

---

## Demo Showcase Scene

The package includes a self-contained dungeon showcase:
- **Scene File**: `Assets/CwcPlugins/CwcSceneDirector/Demo/Demo_SceneDirector.unity`.
- **Keyboard Controls**:
  - Press **I**: Spawns evenly spaced chests across the dungeon;
  - Press **C**: Clears all spawned chests;
  - Press **E**: Starts the dynamic combat wave;
  - Press **K**: Stops the combat wave;
  - Press **X**: Eliminates all enemies (tests victory callback).

---

## License & Commercial Use

- Core source code is licensed under the [Cwc Tools Public License (Source-Available)](LICENSE):
  - **Game Projects (End Products)**: **Free to use in personal and commercial games with zero royalties (Royalty-Free)**.
  - **No Tool Resale**: You may not repackage or resell this software as a standalone asset pack, development tool, or plugin on marketplaces.
- See [Third-Party Notices.txt](Third-Party%20Notices.txt) for details.
