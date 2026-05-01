# Inventory System for Unity

A ready-to-use, grid-based inventory system for Unity with drag-and-drop, tooltips, and a clean API for integration.

This package is designed to be easy to drop into an existing game and extend over time.

## Who This Is For

- Unity developers who want a functional inventory quickly
- Teams that need a reusable inventory module across projects
- Beginners who want a clean starting point without heavy architecture setup

## Features

- Grid inventory (`rows x columns`)
- Add, move, swap, and remove item operations
- Drag-and-drop item interaction
- Hover tooltip system with configurable anchor and delay
- Item validation rules (slot restrictions, custom logic)
- Event-driven core (`ItemAdded`, `ItemRemoved`, `InventoryChanged`, etc.)
- Decoupled API surface via `IInventory`

## Package Structure (High Level)

- `Assets/Scripts/Inventory/Core`  
  Core domain and API contracts (`IInventory`, `InventoryService`, `InventoryFactory`, items, rules, events)
- `Assets/Scripts/Inventory/UI`  
  UI presentation and interaction controllers (slots, drag, tooltip)
- `Assets/Scripts/Inventory/Bootstrap`  
  Scene wiring component (`InventoryInstaller`)
- `Assets/Scripts/Inventory/Data`  
  Item definition assets
- `Assets/Scripts/Inventory/Examples`  
  Starter usage examples

## Quick Start (Scene Setup)

1. Add your inventory UI objects to a Canvas:
   - Grid root
   - Slot template (`InventorySlotView`)
   - Drag icon (`Image`)
   - Tooltip panel + title + description labels
2. Add `InventoryGridLayoutController` to the grid object.
3. Configure `columns`, `rows`, and `spacing`.
4. On `InventoryInstaller`, open the component context menu and click **Populate Slots**.
5. Add `InventoryInstaller` to a scene object.
6. Assign required references in the installer inspector:
   - `canvas`, `gridRoot`, `gridController`, `dragIcon`
   - `tooltipPanel`, `tooltipTitle`, `tooltipDescription`
7. Press Play.

## How to Add Items

At runtime, call inventory operations through the installer:

```csharp
using InventorySystem.Bootstrap;
using InventorySystem.Core;
using UnityEngine;

public sealed class AddItemExample : MonoBehaviour
{
    [SerializeField] private InventoryInstaller installer;

    public void AddMaterial()
    {
        var inventory = installer.Service;
        var item = new MaterialItem("wood_01", "Wood", "Basic crafting material", "", "crafting");
        inventory.TryAddItem(item, out _);
    }
}
```

## Core API (Most Important)

- `IInventory.TryAddItem(IInventoryItem item, out int placedIndex)`
- `IInventory.TryMoveItem(int fromIndex, int toIndex)`
- `IInventory.TrySwapItems(int firstIndex, int secondIndex)`
- `IInventory.RemoveItem(int index)`
- `IInventory.GetItem(int index)`
- `IInventory.Grid`
- Events:
  - `ItemAdded`
  - `ItemMoved`
  - `ItemsSwapped`
  - `ItemRemoved`
  - `InventoryChanged`

## Integration Styles

### 1) Scene-driven (easiest)

Use `InventoryInstaller` and access `installer.Inventory`.

### 2) Core-only (non-scene, custom host)

Create inventory with factory:

```csharp
using InventorySystem.Core;

var inventory = InventoryFactory.Create(columns: 6, rows: 5);
```

## Example Definitions

Dummy equipment definitions for the sample scene are in:

- `Assets/Examples/ItemDefinitions`

These assets intentionally have no textures so you can attach your own icons quickly.

## Creating Custom Rules

Implement `IItemValidationRule`:

```csharp
using InventorySystem.Core;

public sealed class EvenSlotOnlyRule : IItemValidationRule
{
    public bool CanPlace(int slotIndex, IInventoryItem item, InventoryService inventoryService)
    {
        return slotIndex % 2 == 0;
    }
}
```

Then pass rules when creating inventory:

```csharp
var rules = new IItemValidationRule[] { new EvenSlotOnlyRule() };
var inventory = InventoryFactory.Create(6, 5, rules);
```

## Requirements

- Unity project with UI support
- EventSystem in scene (auto-created by installer if missing)
- TextMeshPro package (used by tooltip and labels)

## Troubleshooting

- **No slots visible**  
  Ensure `slotTemplate` is assigned in `InventoryGridLayoutController`, then run **Populate Slots** from `InventoryInstaller`.

- **Drag works but icon is not visible**  
  Check `dragIcon` assignment and ensure dragged item has an icon sprite source.

- **Tooltip appears in wrong place**  
  Verify `tooltipAnchor` and canvas setup; tooltip anchor is mouse-referenced.

## Documentation

- Beginner guide: this `README.md`
- Developer integration reference: `Assets/Scripts/Inventory/INTEGRATION.md`
- Example consumer script: `Assets/Scripts/Inventory/Examples/InventoryConsumerExample.cs`

## UnityPackage Export Checklist

For a clean package export:

1. Open `Assets/Scenes/SampleScene.unity` and confirm the inventory works.
2. Verify `InventoryInstaller` references are assigned and slots are populated.
3. Select these folders/assets for export:
   - `Assets/Scripts/Inventory`
   - `Assets/Prefabs/InventoryUI.prefab`
   - `Assets/Prefabs/SlotTemplate.prefab`
   - `Assets/Scenes/SampleScene.unity`
   - `Assets/Examples/ItemDefinitions`
4. Use `Assets > Export Package...` and enable **Include dependencies**.

## License / Usage

Use and adapt freely inside your game/project.  
If distributing as an Asset Store package, add your preferred license text here.
