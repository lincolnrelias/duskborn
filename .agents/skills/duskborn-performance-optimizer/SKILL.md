---
name: duskborn-performance-optimizer
description: >-
  Use this skill to audit and prevent memory leaks and optimize CPU, GPU, and managed memory (GC) in Duskborn (Unity 6 URP, FishNet, cooperative roguelite with enemy hordes).
---

# Duskborn Performance and Memory Optimizer

This guide establishes procedures and technical standards to eliminate memory leaks, minimize heap allocations (GC Alloc), and maximize CPU and GPU performance in **Duskborn** (Unity 6.000.4 LTS, Universal Render Pipeline 17.4, FishNet P2P).

---

## 1. Quick Coding Guidelines (Golden Rules)

1. **Zero GC in the Game Loop (`Update`, `FixedUpdate`, `LateUpdate`)**:
   - Never use `new` for collections (`List`, `HashSet`, `Dictionary`), arrays, or classes in continuous update methods.
   - Replace allocating physics APIs (`Physics.OverlapSphere`, `Physics.RaycastAll`) with safe reusable-buffer variants (`Physics.OverlapSphereNonAlloc`, `Physics.RaycastNonAlloc`).
   - Avoid LINQ (`.Where()`, `.Select()`, `.ToList()`, etc.) and dynamic string concatenation in frequently executed code.

2. **Strict Memory Leak Prevention**:
   - Explicitly unsubscribe every delegate or event subscription (`+=`) with `-=` in `OnDisable()`, `OnDestroy()`, or pool recycling.
   - Unsubscribe FishNet synchronized variables (`SyncVar<T>.OnChange`) and network callbacks when the object is disabled / destroyed.
   - Never call `renderer.material` at runtime without destroying the created instance afterward; use `renderer.sharedMaterial` or `MaterialPropertyBlock` to maintain **SRP Batcher** compatibility.

3. **Night Horde Optimization (Night Waves)**:
   - Never perform target searches (`PlayerRegistry.FindNearest`) or navigation path updates (`Agent.SetDestination`) for hundreds of enemies every frame.
   - Apply staggered update rates (staggered ticks / jittered intervals) and disable secondary shadows / animations on small enemies (Swarmer).
   - Reuse entities through Object Pooling (`EnemyPool`, `DamageNumberPool`) instead of continuous `Instantiate` and `Destroy` during nights.

---

## 2. Step-by-Step Audit and Optimization Workflow

When inspecting, refactoring, or creating project components, follow these steps:

### Step 1: Inspect Lifecycle and Event Subscriptions
- Check whether classes inheriting from `MonoBehaviour` or `NetworkBehaviour` register listeners for:
  - Static events (`PlayerRegistry`, `GameStateManager`, etc.).
  - Local instance events (`OnDied`, `OnHealthChanged`).
  - FishNet events (`SyncVar.OnChange`, `ServerManager`, `TimeManager`).
- **Action**: Ensure `OnDisable()` or `OnDestroy()` explicitly removes every listener.

### Step 2: Check Hidden Allocations (GC Alloc)
- Look for:
  - `new WaitForSeconds(t)` in frequent coroutines (cache instances or use manual timers with `Time.deltaTime`).
  - Enum boxing or logging calls such as `DuskLog.Log($"...")` executed without a prior conditional check.
  - Returned array allocations in collision queries or calls such as `GetComponentsInChildren<T>()` inside loops.
- **Reference**: [Leak Prevention and Memory Management](./references/memory-leaks-prevention.md).

### Step 3: Optimize CPU in Enemy and AI Systems
- If the script affects enemies (`EnemyBase`, `WaveManager`, `EnemyRagdoll`):
  - Ensure player detection uses squared distances (`sqrMagnitude`) or spaced ticks (e.g. every 0.2s - 0.5s).
  - Ensure offscreen or distant enemies do not update ragdolls or unnecessary animation calculations.
- **Reference**: [CPU and Horde Optimization](./references/cpu-wave-optimization.md).

### Step 4: Optimize GPU and Render Pipeline (URP 17)
- Check outline (*Linework Lite* / `RenderingLayerMask`) and material impact:
  - The base shader must remain SRP Batcher compatible (`SRP Batcher: compatible`).
  - Disable shadow casting (`Cast Shadows = Off`) on dense mobs (Swarmer / Runner).
  - Separate static interface `Canvas` elements from dynamic UI (floating damage numbers and health bars) to avoid rebuilding the UI graphics mesh (*canvas dirtying*).
- **Reference**: [GPU and URP Optimization](./references/gpu-urp-rendering.md).

### Step 5: Validate without Blocking the Unity Editor
- Before committing or validating changes, ensure the Unity Editor is not blocked:
  - Never run Unity CLI commands while the engine is open.
  - Compile offline using internal tools (`dotnet build`, or `Tools/unity.ps1` validation scripts when the Editor is closed).

---

## 3. Detailed Reference Documentation

- [Memory Leak Prevention and GC Alloc](./references/memory-leaks-prevention.md)
- [CPU Optimization for Hordes and Night AI](./references/cpu-wave-optimization.md)
- [GPU, URP Shader, and Linework Lite Optimization](./references/gpu-urp-rendering.md)
