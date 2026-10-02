# Implementation Guide: Integrating Resources, Chests, and POIs into Procedural Terrain (Duskborn)

> **Audience:** Implementation Agent / Unity Developer
> **Objective:** Integrate procedural deterministic generation of resource nodes (trees, rocks, iron, fiber), distance-scaled chests, and points of interest (spawn clearing, workbench, sanctuaries) into the *low-poly chunk* terrain mesh for a cooperative *roguelike* experience (1–5 players).
> **Design Basis:** GDD §5 (Loot & Crafting), §7 (World & Map), and `terrain_generation_handout.md`.

---

## 1. Overview and Roguelike Game Design Pillars

In *Duskborn*, daytime exploration races against nightfall. Resource and item distribution must create meaningful strategic **Risk vs. Reward** decisions:

1. **Safe Central Clearing (Spawn Hub):**
   - The map center $(0,0)$ is guaranteed flat and safe, without excessive tree density or rock barriers.
   - Contains player spawn points, the starting crafting *Workbench*, and enough basic resources for the first primitive *Tier 1* items.
   - Contains 1–2 basic low-cost chests ($25\text{--}50\text{g}$) for an immediate objective.

2. **Distance Gradient (Risk / Reward Radius):**
   - **Center (Safety):** Abundant Wood and Stone; cheap chests containing Common items.
   - **Middle Zone (Forests and Plains):** Higher resource density, Fiber shrubs, medium chests ($75\text{--}100\text{g}$) with Uncommon / Rare item chances.
   - **Edges and Mountain Peaks (High Risk):** Iron Ore nodes, dangerous ravines, golden / legendary chests ($150\text{--}250\text{g}$) with Rare, Legendary, or Cursed items. Being far from the center at nightfall forces the team to fight on rough terrain or run back.

3. **Deterministic Seed Generation (Multiplayer Sync):**
   - All clients calculate tree, rock, and chest positions from the same deterministic seed (`SeededRNG` / `customSeed`), synchronizing vegetation coordinates without network traffic.
   - Interactive destructible entities (`ResourceNode`, `Chest`) are managed with Host authority through *FishNet* (`[ServerRpc]`, `SyncVar`, `LootManager`).

---

## 2. Technical System Architecture

```text
ChunkGridManager (Terrain)
  │
  ├── 1. Generate each Chunk's terrain mesh (TerrainChunk + MeshCollider)
  │
  └── 2. WorldPropsPlacer (New Props & Spawn Manager)
        ├── Initialize SeededRNG with the session seed
        ├── Vertical raycast against MeshColliders for exact terrain placement
        │
        ├── [A] Central Clearing (Map Center)
        │     ├── Clear the central radius
        │     ├── Define PlayerSpawnPoints
        │     └── Instantiate Workbench Site
        │
        ├── [B] Resource Distribution (Resource Nodes)
        │     ├── Forests / Low Grass: Trees (Wood) + Fiber
        │     ├── Rock / Altitude Zones: Rocks (Stone) + Iron Ore
        │     └── Steep Slopes: Decorative boulders / Passage blockers
        │
        ├── [C] Chest Distribution
        │     ├── Grid jitter / Poisson-disc sampling (avoid crowding)
        │     └── Scale cost and loot table by distance from center
        │
        └── 3. RebuildNavMesh() (Include trees, rocks, and chests in NavMesh recalculation)
```

---

## 3. Biome Mapping and Placement Rules

Each object type has filtering rules for **Altitude ($Y$)**, **Slope**, and **Distance from Center ($R$)**:

| Prop Type | Prefab / Component | Altitude / Terrain Condition | Maximum Slope | Distance / Density Rule |
|---|---|---|---|---|
| **Tree (Wood)** | `ResourceNode` (`TargetType.Tree`) | $Y > \text{waterLevel} + 0.5\text{m}$ and $Y < \text{snowLevel}$ | $\le 25^\circ$ (walkable terrain) | High density in grass areas; outside the central spawn radius |
| **Rock (Stone)** | `ResourceNode` (`TargetType.MiningNode`) | Any $Y > \text{waterLevel} + 0.3\text{m}$ | $\le 45^\circ$ | Medium density; more common in rocky zones |
| **Iron Ore** | `ResourceNode` (`TargetType.MiningNode`) | $Y \ge \text{heightMultiplier} \times 0.45$ or slopes | $\le 40^\circ$ | Rare; only at altitude or distant edges |
| **Shrub (Fiber)** | `ResourceNode` / Fiber Pickup | Plains / grass zones | $\le 20^\circ$ | Scattered across open plains |
| **Common Chest** | `Chest.prefab` (Common / Uncommon Loot) | Dry ground ($Y > \text{waterLevel} + 0.5\text{m}$) | $\le 15^\circ$ | Central to middle radius ($R \le 40\text{m}$); Cost: $35\text{--}50\text{g}$ |
| **Advanced / Gold Chest** | `Chest.prefab` (Rare / Legendary Loot) | Dry ground, hilltops, or edges | $\le 20^\circ$ | Outer radius ($R > 40\text{m}$); Cost: $80\text{--}150\text{g}$ |
| **Workbench** | `Workbench` prefab | Central clearing | $\le 5^\circ$ (flat) | 1 instance near player spawn |

---

## 4. Proposed File Structure

```text
Assets/_Duskborn/
├── Gameplay/
│   ├── World/
│   │   ├── Props/
│   │   │   ├── PropDefinition.cs       // SO: Prefab, density, height / slope rules, exclusion radius
│   │   │   ├── WorldPropsConfig.cs     // SO: Prop list, chests by tier, clearing radius
│   │   │   └── WorldPropsPlacer.cs     // Component sampling and spawning props
│   │   ├── LowPolyTerrainConfig.cs     // (Already implemented)
│   │   ├── TerrainChunk.cs             // (Already implemented)
│   │   └── ChunkGridManager.cs         // (Updated to call WorldPropsPlacer before NavMesh)
│   ├── Loot/
│   │   ├── Chest.cs                    // (Already implemented with SyncVar and TargetRpc)
│   │   ├── ResourceNode.cs             // (Already implemented with TargetType and LootDropper)
│   │   └── LootTable.cs / DropLootTable.cs // (Already implemented)
│   └── Crafting/
│       └── Workbench.cs                // Simple workbench interaction component
└── ScriptableObjects/
    └── World/
        ├── PropsConfig_Default.asset   // Default trees, rocks, iron, and chest configuration
        └── ...
```

---

## 5. Technical Specification of New Scripts

### 5.1 `PropDefinition.cs` (ScriptableObject)
Define ecological rules for each decorative or interactive element:

```csharp
using UnityEngine;

namespace Duskborn.Gameplay.World
{
    [CreateAssetMenu(fileName = "Prop_Name", menuName = "Duskborn/World/Prop Definition")]
    public class PropDefinition : ScriptableObject
    {
        public string propName = "Tree";
        public GameObject prefab;

        [Header("Density per Chunk")]
        [Range(0, 50)] public int minPerChunk = 2;
        [Range(0, 50)] public int maxPerChunk = 6;

        [Header("Terrain Conditions")]
        public float minHeight = 2.5f;
        public float maxHeight = 20.0f;
        [Range(0f, 60f)] public float maxSlopeAngle = 25f;

        [Header("Scale and Rotation Variation")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.25f);
        public bool randomYRotation = true;
        public bool alignToNormal = false;

        [Header("Spacing")]
        [Tooltip("Minimum distance radius from other props")]
        public float exclusionRadius = 2.0f;
    }
}
```

---

### 5.2 `WorldPropsConfig.cs` (ScriptableObject)
Group all resource, chest, and clearing rule definitions:

```csharp
using UnityEngine;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.World
{
    [CreateAssetMenu(fileName = "WorldPropsConfig", menuName = "Duskborn/World/World Props Config")]
    public class WorldPropsConfig : ScriptableObject
    {
        [Header("Central Clearing (Safe Spawn Zone)")]
        [Tooltip("Radius around (0,0) where dense trees or rocks are not generated")]
        public float centerClearingRadius = 10f;
        public GameObject workbenchPrefab;

        [Header("Natural Resources (Resource Nodes)")]
        public PropDefinition treeProp;
        public PropDefinition stoneProp;
        public PropDefinition ironProp;
        public PropDefinition fiberProp;

        [Header("Chest Configuration")]
        public GameObject chestPrefab;
        [Range(1, 20)] public int totalChests = 8;
        public LootTable basicLootTable;
        public LootTable rareLootTable;
        public int minChestCost = 35;
        public int maxChestCost = 150;
    }
}
```

---

### 5.3 `WorldPropsPlacer.cs` (Monobehaviour)
Run procedural generation using **Raycasts** against the newly generated mesh:

1. **Jittered Grid Sampling:** Divide each *chunk* into subcells and apply deterministic jitter using `SeededRNG`.
2. **Terrain Validation:** Cast `Physics.Raycast` downward ($Y = 100 \rightarrow -10$).
   - Obtain `hit.point` (exact face height) and `hit.normal` (slope).
   - Validate that `hit.point.y` and `slopeAngle` satisfy `PropDefinition`.
   - Reject points inside `centerClearingRadius` (except clearing items).
3. **Chest Scaling:**
   - Calculate distance $d = \text{Vector3.Distance}(pos, \text{Vector3.zero})$.
   - Normalize $t = \text{Clamp01}(d / \text{mapRadius})$.
   - Chest cost: $\text{Lerp}(minCost, maxCost, t)$.
   - Assign `basicLootTable` if $t < 0.5$ or `rareLootTable` if $t \ge 0.5$.
4. **Instantiation:** In *Host / Singleplayer*, spawn objects containing `NetworkObject` through `InstanceFinder.ServerManager.Spawn(go)`.

---

## 6. `ChunkGridManager` Integration and Execution Order

The complete generation cycle becomes:

```csharp
public void GenerateGrid()
{
    // 1. Clear old terrain and props.
    ClearGrid();
    ClearProps();

    // 2. Generate Chunks and meshes with MeshColliders.
    GenerateTerrainChunks();

    // 3. Spawn resource nodes, chests, and the clearing.
    if (propsPlacer != null)
    {
        propsPlacer.PlaceWorldProps(config, propsConfig, activeSeed);
    }

    // 4. Bake NavMesh including terrain meshes and prop colliders.
    RebuildNavMesh();
}
```

---

## 7. Step-by-Step Instructions for the Implementing Agent

1. **Create Scripts:**
   - Create `PropDefinition.cs`, `WorldPropsConfig.cs`, and `WorldPropsPlacer.cs` in `Assets/_Duskborn/Gameplay/World/Props/`.
   - Update `ChunkGridManager.cs` and `ChunkGridManagerEditor.cs` to support the new spawning step.
2. **Create ScriptableObjects:**
   - Create `Prop_Tree.asset`, `Prop_Stone.asset`, `Prop_Iron.asset`, and `Prop_Fiber.asset`.
   - Create `WorldPropsConfig_Default.asset` linking `ResourceNode` and `Chest` prefabs.
3. **Validation Tests:**
   - Manual user check: click **"Generate Terrain"** in the Inspector. Agents use the project's non-interactive CLI.
   - Verify trees and rocks are grounded in the terrain without floating or sinking.
   - Verify center $(0,0)$ remains clear and contains the workbench.
   - Verify NavMesh is baked around trunks and chests correctly.
