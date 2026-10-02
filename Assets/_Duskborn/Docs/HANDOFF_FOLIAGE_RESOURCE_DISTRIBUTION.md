# Handoff: Unified Resource and Foliage Distribution System

> **Game Context**: *Duskborn* is a cooperative survival / extraction game with sessions averaging **1 to 2 hours**.
> **Objective**: Implement a cohesive spatial distribution architecture for both **Interactive Resource Nodes** (trees, rocks, iron, fiber) and **Pure Visual Foliage** (grass, decorative shrubs), with granular density, clustering, internal spacing (*Poisson / Clearance*), and strict overlap prevention, preserving fast pacing and clear combat flow.

---

## 1. Critical Assessment & Separation of Responsibilities

### ⚠️ Required Architectural Distinction
1. **Interactive Resource Nodes (Harvestable Resource Nodes)**:
   - Implement `ResourceNode.cs`, `NetworkObject` (FishNet), and `IDamageable`; have physical `Collider` components, HP synchronized through `SyncVar`, floating damage, and item drops on depletion.
   - **MUST NOT** merge into the static foliage mesh batch (`ChunkFoliagePlacer`). They must remain server-authoritative network instances managed by `WorldPropsPlacer`.
2. **Pure Visual Foliage (Decorative Foliage)**:
   - Purely aesthetic grass and shrubs rendered with `Duskborn/StylizedFoliage`, blending vertices into terrain.
   - Generated on clients without collision and combined into 1 draw call per chunk for maximum performance.
3. **Unified Solution**:
   - A shared **Spatial Occupancy & Clustering Coordinator**. The server / generator first places interactive resources in organic pockets and publishes an occupancy map / mask. Decorative foliage then fits around trunks and rocks without intersecting models, naturally growing denser beneath tree canopies.

---

## 2. Game Design Vision (1 to 2 Hour Sessions)

For medium-length sessions (60–120 minutes), traditional uniform distribution is frustrating and harms pacing:
- **Burst Gathering**: Instead of walking 20 meters per tree, the player finds a grove of 4 to 8 clustered trees or a vein of 3 to 5 iron nodes. The player gathers quickly, fills inventory, and advances to action / crafting.
- **Preserved Combat Arenas**: Clustering frees 30% to 40% of the map for clear open areas where players can dodge, kite, and fight hordes without visual obstacles or camera obstruction.
- **Radial Risk / Reward Zoning**:
  - **Zone 0 (Central Sanctuary, 0–12m)**: Clear area, starting workbench, zero obstruction.
  - **Zone 1 (Nearby Outskirts, 12–40m)**: Essential starting resources (common wood, basic stone, fiber). Medium clusters (3–5 nodes) provide starting equipment in the first 5 minutes.
  - **Zone 2 (Central Lands, 40–80m)**: Dense groves and mixed iron / stone veins. Natural gathering and conflict points.
  - **Zone 3 (Plateaus and High Boundaries, 80m+)**: Main concentration of Iron Ore and rare chests, surrounded by elevation changes and enemy patrols.

---

## 3. Technical Component Specification

### A) Data Model: `ResourceClusterConfig` (or an extension to `PropDefinition`)
Add clear clustering controls to the prop definition:
```csharp
[System.Serializable]
public class ResourceClusterSettings
{
    [Tooltip("When enabled, this resource spawns in clustered pockets.")]
    public bool enableClustering = true;

    [Tooltip("Number of resource clusters / pockets per chunk.")]
    [Range(0, 8)] public int clustersPerChunk = 2;

    [Tooltip("Number of nodes generated within one cluster.")]
    public Vector2Int nodesPerCluster = new Vector2Int(3, 6);

    [Tooltip("Cluster spread radius around its center.")]
    [Range(2f, 15f)] public float clusterRadius = 6.0f;

    [Tooltip("Minimum distance between nodes in the same cluster (prevents physical overlap).")]
    [Range(1.2f, 5f)] public float intraClusterSpacing = 2.4f;

    [Tooltip("Minimum isolation distance from other resource types.")]
    [Range(2f, 10f)] public float interClusterSpacing = 5.0f;
}
```

### B) Spatial Coordinator: `WorldPlacementGrid` / `SpatialOccupancyMap`
Create a lightweight two-dimensional spatial structure (1m to 2m cell grid) or an indexed list of occupancy circles:
- **Occupancy Types**:
  - `Resource_Solid`: Physical blocking radius (e.g. tree trunk r = 1.0m; rock r = 1.4m; chest r = 1.2m).
  - `Resource_Canopy`: Canopy influence radius (e.g. r = 4.0m). Used by foliage to generate shaded grass tufts and mushrooms around the tree.
  - `Combat_Clearing`: Reserved combat clearing area without trees or rocks.
  - `Player_Sanctuary`: Central sanctuary and workbench.

### C) Refactor the `WorldPropsPlacer.cs` Pipeline
1. **Step 1 — Generate Cluster Centers**:
   - Calculate cluster centers using Poisson noise constrained by biome and slope.
2. **Step 2 — Intracluster Distribution**:
   - For each cluster center, generate N nodes respecting `intraClusterSpacing` through terrain raycasts.
   - Register position and radius in the spatial occupancy map.
3. **Step 3 — Export Occupancy**:
   - Expose the occupancy map or occupied point list to the chunk foliage layer.

### D) Update `ChunkFoliagePlacer.cs` (Visual Foliage)
1. **Overlap Prevention**:
   - When selecting a grass tuft or decorative shrub coordinate, query the occupancy map:
     - If inside `Resource_Solid` (trunk or rock radius), **reject** the tuft.
     - If in the `Resource_Canopy` ring (tree periphery), grass / shrub spawn probability may **increase** by 50% to simulate lush shade vegetation.
2. **Preserved Performance**:
   - Continue combining all visual chunk foliage into one mesh batch (`UniversalForward`, GPU Instanced, 1 draw call).

---

## 4. Suggested Implementation Plan for the Next Agent

1. **Step 1 — Extend `PropDefinition.cs` / `WorldPropsConfig.cs`**:
   - Include clustering parameters (`clustersPerChunk`, `nodesPerCluster`, `clusterRadius`, `intraClusterSpacing`).
   - Configure the existing ScriptableObjects (`Prop_Tree.asset`, `Prop_Stone.asset`, `Prop_Iron.asset`, `Prop_Fiber.asset`).

2. **Step 2 — Implement Cluster Spawning in `WorldPropsPlacer.cs`**:
   - Replace pure uniform random sampling with pocket-based sampling (Cluster Centers + Local Poisson Disc Sampling).
   - Preserve full FishNet compatibility (`NetworkObject.Spawn`).

3. **Step 3 — Connect `WorldPropsPlacer` and `ChunkFoliagePlacer`**:
   - Pass the occupied obstacle list to `ChunkFoliagePlacer` during chunk generation.
   - Ensure grass and shrubs never intersect tree trunks or stone veins.

4. **Step 4 — Validation & Tuning**:
   - Run `dotnet build Mugg.sln`.
   - Manual user check in the Editor / Play Mode: inspect combat clearings, grove density, and absence of mesh overlap. Agents must follow the non-interactive CLI workflow.
