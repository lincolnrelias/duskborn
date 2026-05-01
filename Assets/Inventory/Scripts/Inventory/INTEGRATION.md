# Inventory Integration Guide

This package now exposes a stable, host-friendly API:

- `IInventoryReader`: read operations
- `IInventoryWriter`: mutation operations
- `IInventory`: full contract (read + write + events)

## Contracts You Should Depend On

- Use `IInventory` in gameplay systems, services, and UI adapters.
- Avoid depending on `InventoryService` directly unless you need concrete-only behavior.
- Keep `InventoryInstaller` as the scene wiring layer.

## Quick Start (Core-Only)

```csharp
using InventorySystem.Core;

var inventory = InventoryFactory.Create(columns: 6, rows: 5);
inventory.InventoryChanged += () => { /* refresh UI */ };

inventory.TryAddItem(new MaterialItem("wood_01", "Wood", "Basic material", "", "crafting"), out _);
```

## Quick Start (Unity Scene + Installer)

`InventoryInstaller` keeps backward compatibility and now also exposes:

- `Service` (existing concrete type)
- `Inventory` (`IInventory` contract)

Use `Inventory` when integrating from other systems to reduce coupling:

```csharp
var inventory = installer.Inventory;
inventory.TryMoveItem(0, 4);
```

## Example Consumer Script

See `Assets/Scripts/Inventory/Examples/InventoryConsumerExample.cs`.

What it demonstrates:

- Reading inventory through `IInventory` only
- Subscribing/unsubscribing to `ItemAdded` and `ItemRemoved`
- Triggering an add operation from a context menu

## Common Operation Examples

### Add Item

```csharp
var item = new EquipmentItem("sword_01", "Sword", "Starter sword", "", 10);
var added = inventory.TryAddItem(item, out var slotIndex);
```

### Move Item

```csharp
var moved = inventory.TryMoveItem(fromIndex: 0, toIndex: 5);
```

### Swap Items

```csharp
var swapped = inventory.TrySwapItems(firstIndex: 1, secondIndex: 7);
```

### Remove Item

```csharp
var removed = inventory.RemoveItem(index: 3);
```

### Read Slots

```csharp
for (var i = 0; i < inventory.Grid.SlotCount; i++)
{
    var item = inventory.GetItem(i);
    // null means empty slot
}
```

## Event Integration Example

```csharp
inventory.ItemAdded += evt =>
{
    Debug.Log($"Added: {evt.Item.DisplayName} at slot {evt.SlotIndex}");
};

inventory.ItemsSwapped += evt =>
{
    Debug.Log($"Swapped slots {evt.FirstIndex} and {evt.SecondIndex}");
};
```

## Custom Validation Rule Example

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

```csharp
var rules = new IItemValidationRule[] { new EvenSlotOnlyRule() };
var inventory = InventoryFactory.Create(columns: 6, rows: 5, rules: rules);
```

## Migration Tip For Other Projects

- Replace direct `InventoryService` references with `IInventory`.
- Keep construction in one place (`InventoryFactory` or scene installer).
- Route UI/gameplay systems through injected `IInventory` references.

## What Is Already Good In This Package

- Core model (`InventoryGrid`, `IInventoryItem`, rules, events) is decoupled from UI.
- Mutation methods already return `bool` and are safe for host orchestration.
- Events provide reactive integration points without polling.

## Recommended Integration Pattern

- Depend on `IInventory` in gameplay systems.
- Keep Unity scene wiring (`InventoryInstaller`, drag/tooltip/presenter) as adapter layer only.
- Use `IItemValidationRule` implementations for project-specific placement constraints.
