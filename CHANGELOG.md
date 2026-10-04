# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-04

### Added
- **Core Scheduler & Modular Lifecycle**: Pure scheduling hub (`CwcSceneDirector`) with priority sorting, lazy instantiation, and pure C# module abstraction (`CwcSceneDirectorModule`) supporting cooperative coroutines and runtime pause/resume.
- **Diablo IV-Style Two-Level Placement & Quota Deck**: Macro candidate sampling paired with micro cluster dispersion (`CwcWeightedPlacementModule`). Implemented quota deck algorithm with guaranteed minimum limits, proportional weight distribution, hard ceilings, and Fisher-Yates shuffling.
- **Risk of Rain 2-Inspired Credit AI Director**: Dynamic encounter spawning module (`CwcCreditDirectorModule`) featuring Target-Lock anti-starvation mechanism, soft-cap overdraft for complete squad purchases, unidirectional active-entity budget consumption, wave breathing cooldown, and concurrent cost limits.
- **Sunflower / Vogel Golden Spiral Micro Formations**: Uniform radial squad dispersal algorithm with $137.5^\circ$ golden angle stepping, elastic inward bouncing upon obstacle collisions, and fallback平地补齐 ("No Monster Left Behind").
- **Spacious Farthest Point Sampling (Spacious-FPS)**: Grid-based mathematical topology sampling in `CwcSceneSpatialManager`, weighting local footprint density with farthest point distance to place clusters in room centers without raycasting overhead.
- **Physical Footprint Auto-Detection & Ledge Safety Probing**: Automatically extracts base clearance radius from native `Collider`, `CharacterController`, or `Renderer` bounds without marker interfaces. Four-way downward probes prevent units from spawning on precipices or over voids.
- **Spatial Hash Grid & Entity Management**: Lightweight 2D horizontal hash grid (`CwcSpatialGrid`) providing $O(1)$ radius and threat cost queries. `CwcSceneEntityManager` features automated prefab-categorized pooling and distance-based culling with grace periods.
- **Time-Sliced Frame Budgeting**: Integrated high-precision `Stopwatch` in `CwcPlacementContext`, dynamically yielding control (`yield return null`) only when frame budget (default 2.0ms) is exceeded, preventing frame drops.
- **Editor Tooling & Custom Drawers**: Compact card drawer (`CwcPlacementItemDrawer`) with type-based accent colors, inline weight inputs, foldouts, and script jump navigation. `CwcSceneDirectorEditor` and `CwcSceneEntityManagerEditor` provide live runtime monitoring and debugging controls.
- **Self-Contained Dungeon Demo**: Complete showcase scene with player controls, camera follow, chest/interactable placement, and altar encounter triggers.
