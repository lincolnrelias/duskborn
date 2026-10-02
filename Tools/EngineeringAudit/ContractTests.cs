// Compiles production recipe/inventory/status code. Unity/FishNet plumbing is shimmed;
// this does not exercise native rendering, serialization, transport or SyncVar weaving.
using System;
using System.Collections.Generic;
using System.Reflection;
using Duskborn.Gameplay.Combat;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Loot;
using InventorySystem.Core;
using InventorySystem.Data;

class EngineeringAuditTests
{
    static int passed;
    static void Check(bool condition, string scenario)
    {
        if (!condition) throw new Exception(scenario);
        Console.WriteLine("PASS " + scenario); passed++;
    }
    static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void Tick(StatusEffectController effects) =>
        typeof(StatusEffectController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effects, null);
    sealed class Item : IInventoryItem { }
    sealed class Reject : IItemValidationRule
    {
        public bool CanPlace(int index, IInventoryItem item, InventoryService inventory) => false;
    }
    static CraftingRecipe Recipe(ItemDefinitionBase output)
    {
        var recipe = new CraftingRecipe();
        Set(recipe, "outputItem", output);
        var wood = new MaterialDefinition { Id = "wood" };
        Set(recipe, "ingredients", new List<CraftingIngredient> {
            new() { material = wood, amount = 2 }, new() { material = wood, amount = 3 } });
        return recipe;
    }
    static ResourceInventory Wallet() { var wallet = new ResourceInventory(); wallet.Add("wood", 10); return wallet; }
    static void Main()
    {
        try { Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); Environment.Exit(1); }
    }
    static void Run()
    {
        var output = new ItemDefinitionBase { Id = "axe", Factory = () => new Item() };
        var recipe = Recipe(output);
        var wallet = Wallet(); var inventory = new InventoryService(new InventoryGrid(1, 1));
        Check(recipe.TryCraftItem(wallet, inventory) && wallet.GetCount("wood") == 5 && inventory.GetItem(0) != null,
            "successful craft delivers one item and pays aggregated costs once");
        Check(!recipe.TryCraftItem(wallet, inventory) && wallet.GetCount("wood") == 5,
            "full inventory retains ingredients");
        wallet = Wallet();
        Check(!recipe.TryCraftItem(wallet, null) && wallet.GetCount("wood") == 10, "missing inventory retains ingredients");
        inventory = new InventoryService(new InventoryGrid(1, 1));
        Check(!Recipe(new ItemDefinitionBase()).TryCraftItem(wallet, inventory) && wallet.GetCount("wood") == 10,
            "null factory output retains ingredients");
        Check(!Recipe(null).TryCraftItem(wallet, inventory) && wallet.GetCount("wood") == 10,
            "missing output definition retains ingredients");
        inventory = new InventoryService(new InventoryGrid(1, 1), new[] { new Reject() });
        Check(!recipe.TryCraftItem(wallet, inventory) && wallet.GetCount("wood") == 10,
            "inventory validation rule rejection retains ingredients");
        inventory = new InventoryService(new InventoryGrid(1, 1));
        var occupiedInventory = inventory;
        bool filled = false;
        wallet.ResourceChanged += (id, amount) => {
            if (!filled) { filled = true; occupiedInventory.TryAddItem(new Item(), out _); }
        };
        Check(!recipe.TryCraftItem(wallet, inventory) && wallet.GetCount("wood") == 10 && inventory.GetItem(0) != null,
            "slot filled by payment callback refunds duplicate ingredient totals");
        wallet = new ResourceInventory(); wallet.Add("wood", int.MaxValue);
        int revision = wallet.Revision; bool overflow = false;
        try { wallet.Add("wood", 1); } catch (OverflowException) { overflow = true; }
        Check(overflow && wallet.GetCount("wood") == int.MaxValue && wallet.Revision == revision,
            "overflowing add leaves wallet and revision unchanged");

        var effects = new StatusEffectController { IsServerStarted = true };
        UnityEngine.Time.time = 10;
        effects.ServerApply(StatusEffect.Invulnerable, 0);
        effects.ServerRemove(StatusEffect.Invulnerable);
        effects.ServerApply(StatusEffect.Invulnerable, 2);
        UnityEngine.Time.time = 12; Tick(effects);
        Check(!effects.Has(StatusEffect.Invulnerable), "removed indefinite effect can be reapplied with finite duration");
        UnityEngine.Time.time = 20; effects.ServerApply(StatusEffect.Burn, 10);
        effects.ServerRemove(StatusEffect.Burn); effects.ServerApply(StatusEffect.Burn, 1);
        UnityEngine.Time.time = 21; Tick(effects);
        Check(!effects.Has(StatusEffect.Burn), "removed long effect does not extend a new short application");
        UnityEngine.Time.time = 30; effects.ServerApply(StatusEffect.Slow, 2);
        UnityEngine.Time.time = 31; effects.ServerApply(StatusEffect.Slow, 3);
        UnityEngine.Time.time = 32; Tick(effects);
        Check(effects.Has(StatusEffect.Slow), "reapplying an active effect still extends its deadline");
        UnityEngine.Time.time = 34; Tick(effects);
        Check(!effects.Has(StatusEffect.Slow), "extended active effect expires at its new deadline");
        UnityEngine.Time.time = 40; effects.ServerApply(StatusEffect.Burn | StatusEffect.Stun, 0);
        effects.ServerRemove(StatusEffect.Burn); effects.ServerApply(StatusEffect.Burn, 1);
        UnityEngine.Time.time = 41; Tick(effects);
        Check(!effects.Has(StatusEffect.Burn) && effects.Has(StatusEffect.Stun), "removing one flag preserves another indefinite effect");
        Console.WriteLine($"{passed} engineering regression tests passed.");
    }
}
namespace UnityEngine { public static class Time { public static float time; } }
namespace FishNet.Object { public class NetworkBehaviour : UnityEngine.MonoBehaviour { public bool IsServerStarted; } }
namespace FishNet.Object.Synchronizing
{
    public class SyncVar<T>
    {
        private T current;
        public event Action<T, T, bool> OnChange;
        public T Value { get => current; set { var previous = current; current = value; OnChange?.Invoke(previous, value, true); } }
    }
}
namespace Duskborn.Gameplay.Combat { public class StatusEffectVisuals { } }
namespace Duskborn.Core { }
static class DuskLog { public static void Log(LogChannel channel, string text) { } }
enum LogChannel { Combat }
