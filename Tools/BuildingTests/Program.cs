// Engine-free contract tests compile the real inventory, costs, recipe and processing source.
// The shim replaces only Unity object/attribute plumbing; physics/network/UI require Editor QA.
using System;
using System.Collections.Generic;
using System.Reflection;
using Duskborn.Gameplay.Building;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Loot;
using InventorySystem.Data;
class Program
{
    static int count;
    static void Assert(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
    static void Set(object o, string field, object value) => o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    static void Main()
    {
        var wood = new MaterialDefinition { Id = "wood" }; var ore = new MaterialDefinition { Id = "ore" };
        var inventory = new ResourceInventory(); inventory.Add("wood", 10); inventory.Add("ore", 2);
        var repeated = new List<CraftingIngredient> { new() { material = wood, amount = 6 }, new() { material = wood, amount = 6 } };
        Assert(!MaterialCosts.Spend(inventory, repeated) && inventory.GetCount("wood") == 10, "duplicate ingredient totals cannot overspend");
        Assert(!inventory.TrySpend("wood", -1) && inventory.GetCount("wood") == 10, "negative spending cannot mint resources");
        Assert(!inventory.TrySpend("wood", 0), "zero spending rejected");
        Assert(!inventory.TrySpendBatch(new Dictionary<string,int>{{"wood", 3},{"ore",3}}) && inventory.GetCount("wood") == 10, "failed multi-material purchase is atomic");
        bool atomic = true;
        inventory.ResourceChanged += (id, n) => { if (inventory.GetCount("ore") != 0 || inventory.GetCount("wood") != 7) atomic = false; };
        Assert(inventory.TrySpendBatch(new Dictionary<string,int>{{"wood",3},{"ore",2}}) && atomic, "observers see committed batch");
        var bad = new List<CraftingIngredient> { new() { material = null, amount = 2 } };
        Assert(!MaterialCosts.CanPay(inventory,bad), "null material does not become a free cost");
        var recipe = new CraftingRecipe(); Set(recipe,"recipeId","smelt"); Set(recipe,"outputItem",new MaterialDefinition { Id="bar" }); Set(recipe,"outputAmount",1); Set(recipe,"processingSeconds",5f);
        BuildingWorld.Recipes["smelt"] = recipe;
        var definition = new BuildableDefinition { capacity = 1, processingSpeed = 1, station = CraftingStationType.Forja };
        var station = new PlacedBuilding(); var state = new BuildingState(); state.jobs.Add(new ProcessingJob { recipe="smelt", remaining=5 });
        station.Initialize(definition,state); station.Tick(2);
        Assert(state.jobs[0].remaining == 3 && state.contents.Count == 0, "processing consumes elapsed time without early output");
        station.Tick(3); Assert(state.jobs.Count == 0 && state.contents[0].amount == 1, "completed process produces exact output once");
        station.Tick(100); Assert(state.contents[0].amount == 1, "completed process cannot duplicate output");
        state.jobs.Add(new ProcessingJob {recipe="smelt", remaining=5}); station.Tick(30);
        Assert(state.jobs[0].remaining == 5 && state.contents[0].amount == 1, "full output pauses without burning queued work");
        state.contents.Clear(); station.Tick(5); Assert(state.jobs.Count == 0 && state.contents[0].amount == 1, "collecting output resumes processing");
        Assert(!station.Empty, "contents block dismantling"); state.contents.Clear(); Assert(station.Empty,"empty station can be dismantled");
        state.jobs.Add(new ProcessingJob {recipe="smelt",remaining=3}); state.jobs.Add(new ProcessingJob {recipe="smelt",remaining=5}); definition.capacity=10; station.Tick(6);
        Assert(state.contents[0].amount==1 && state.jobs.Count==1 && state.jobs[0].remaining==2, "large ticks carry remaining time into next job");
        station.Tick(-3); Assert(state.jobs[0].remaining==2, "negative time cannot reverse production");
        var restored = new PlacedBuilding(); restored.Initialize(definition,state); restored.Tick(2);
        Assert(state.jobs.Count==0 && state.contents[0].amount==2, "restored progress completes only remaining duration");
        Set(recipe,"ingredients",new List<CraftingIngredient>{new(){material=ore,amount=2}});
        Set(recipe,"fuelIngredients",new List<CraftingIngredient>{new(){material=wood,amount=1}});
        var slottedState = new BuildingState { selectedRecipe="smelt" };
        slottedState.inputs.Add(new MaterialStack{id="ore",amount=4});
        slottedState.fuel.Add(new MaterialStack{id="wood",amount=1});
        var slotted = new PlacedBuilding(); slotted.Initialize(definition,slottedState); slotted.Tick(2);
        Assert(slottedState.jobs.Count==1 && slottedState.jobs[0].remaining==3 && slotted.InputAmount("ore")==2 && slotted.FuelAmount("wood")==0 && slotted.FuelCharges==1,
            "one wood starts the first of two two-ore forge burns");
        slotted.Tick(3);
        Assert(slottedState.contents[0].amount==1 && slottedState.jobs.Count==0,
            "slotted processing produces output after exact duration");
        slotted.Tick(5);
        Assert(slottedState.contents[0].amount==2 && slottedState.jobs.Count==0 && slotted.FuelCharges==0,
            "one wood refines four ore across two separate melts");
        var starvedState = new BuildingState { selectedRecipe="smelt" };
        starvedState.inputs.Add(new MaterialStack{id="ore",amount=2});
        var starved = new PlacedBuilding(); starved.Initialize(definition,starvedState); starved.Tick(10);
        Assert(starvedState.jobs.Count==0 && starved.InputAmount("ore")==2,
            "missing fuel never consumes the input slot");
        Assert(!starved.Empty, "loaded forge slots block dismantling");
        var fullSlottedState = new BuildingState { selectedRecipe="smelt" };
        fullSlottedState.inputs.Add(new MaterialStack{id="ore",amount=2});
        fullSlottedState.fuel.Add(new MaterialStack{id="wood",amount=1});
        fullSlottedState.contents.Add(new MaterialStack{id="bar",amount=10});
        var fullSlotted = new PlacedBuilding(); fullSlotted.Initialize(definition,fullSlottedState); fullSlotted.Tick(10);
        Assert(fullSlottedState.jobs.Count==0 && fullSlotted.InputAmount("ore")==2 && fullSlotted.FuelAmount("wood")==1,
            "full output never consumes loaded input or fuel");
        var forgeWallet = new ResourceInventory(); forgeWallet.Add("ore",2);
        Assert(!recipe.CanCraft(forgeWallet), "recipe affordability includes separate fuel");
        forgeWallet.Add("wood",1);
        Assert(recipe.CanCraft(forgeWallet), "recipe becomes affordable with input and fuel");
        Console.WriteLine($"{count} contract tests passed.");
    }
}
namespace UnityEngine
{
    public class Object { public string name; }
    public class ScriptableObject : Object {}
    public class MonoBehaviour : Object { public Transform transform = new(); }
    public class Transform { public void SetPositionAndRotation(Vector3 p, Quaternion q) {} }
    public class GameObject : Object {}
    public class Texture2D : Object {}
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
    public struct Quaternion { public static Quaternion Euler(float x,float y,float z) => new(); }
    public struct LayerMask { public static implicit operator LayerMask(int n)=>new(); }
    public static class Mathf { public static float Max(float a,float b)=>Math.Max(a,b); public static float Min(float a,float b)=>Math.Min(a,b); }
    public static class Debug { public static void LogException(Exception e) {} }
    public class SerializeField : Attribute {}
    public class Header : Attribute { public Header(string x){} }
    public class Tooltip : Attribute { public Tooltip(string x){} }
    public class TextArea : Attribute {}
    public class Min : Attribute { public Min(float x){} }
    public class RangeAttribute : Attribute { public RangeAttribute(float x,float y){} }
    public class CreateAssetMenu : Attribute { public string menuName,fileName; }
}
namespace InventorySystem.Core { public interface IInventoryItem {} }
namespace InventorySystem.Data
{
    public class ItemDefinitionBase { public string Id, DisplayName, Description; public UnityEngine.Texture2D Icon; public InventorySystem.Core.IInventoryItem CreateRuntimeItem()=>null; }
    public class MaterialDefinition : ItemDefinitionBase {}
}
namespace Duskborn.Gameplay.Crafting { public class RecipeDiscoveryTracker { public bool IsDiscovered(CraftingRecipe r)=>true; } }
namespace Duskborn.Gameplay.Building { public static class BuildingWorld { public static Dictionary<string,CraftingRecipe> Recipes = new(); public static CraftingRecipe Recipe(string id) => Recipes.TryGetValue(id,out var r)?r:null; } }

