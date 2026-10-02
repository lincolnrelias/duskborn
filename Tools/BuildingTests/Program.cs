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
        var definition = new BuildableDefinition { capacity = 1, processingSpeed = 1, station = CraftingStationType.Forge };
        var station = new PlacedBuilding(); var state = new BuildingState(); state.jobs.Add(new ProcessingJob { recipe="smelt", remaining=5 });
        station.Initialize(definition,state); station.Tick(2);
        Assert(station.IsProcessing, "paid queued job burns with no stored fuel");
        Assert(state.jobs[0].remaining == 3 && state.contents.Count == 0, "processing consumes elapsed time without early output");
        station.Tick(3); Assert(state.jobs.Count == 0 && state.contents[0].amount == 1, "completed process produces exact output once");
        station.Tick(100); Assert(state.contents[0].amount == 1, "completed process cannot duplicate output");
        state.jobs.Add(new ProcessingJob {recipe="smelt", remaining=5}); station.Tick(30);
        Assert(!station.IsProcessing, "output blocking extinguishes paid queued work");
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
        Assert(slotted.IsProcessing, "paid slotted job stays active after fuel inventory reaches zero");
        Assert(slottedState.jobs.Count==1 && slottedState.jobs[0].remaining==3 && slotted.InputAmount("ore")==2 && slotted.FuelAmount("wood")==0 && slotted.FuelCharges==1,
            "one wood starts the first of two two-ore forge burns");
        slotted.Tick(3);
        Assert(slotted.IsProcessing, "consecutive burns remain active at an exact job boundary");
        Assert(slottedState.contents[0].amount==1 && slottedState.jobs.Count==0,
            "slotted processing produces output after exact duration");
        slotted.Tick(5);
        Assert(!slotted.IsProcessing, "final recipe completion starts cooldown");
        Assert(slottedState.contents[0].amount==2 && slottedState.jobs.Count==0 && slotted.FuelCharges==0,
            "one wood refines four ore across two separate melts");
        var starvedState = new BuildingState { selectedRecipe="smelt" };
        starvedState.inputs.Add(new MaterialStack{id="ore",amount=2});
        var starved = new PlacedBuilding(); starved.Initialize(definition,starvedState); starved.Tick(10);
        Assert(!starved.IsProcessing, "true fuel starvation is idle");
        Assert(starvedState.jobs.Count==0 && starved.InputAmount("ore")==2,
            "missing fuel never consumes the input slot");
        Assert(!starved.Empty, "loaded forge slots block dismantling");
        var fullSlottedState = new BuildingState { selectedRecipe="smelt" };
        fullSlottedState.inputs.Add(new MaterialStack{id="ore",amount=2});
        fullSlottedState.fuel.Add(new MaterialStack{id="wood",amount=1});
        fullSlottedState.contents.Add(new MaterialStack{id="bar",amount=10});
        var fullSlotted = new PlacedBuilding(); fullSlotted.Initialize(definition,fullSlottedState); fullSlotted.Tick(10);
        Assert(!fullSlotted.IsProcessing, "full output blocks eligible slotted burn");
        fullSlottedState.contents.Clear();
        Assert(fullSlotted.IsProcessing, "collecting output resumes eligible slotted burn");
        int inputBefore = fullSlotted.InputAmount("ore"), fuelBefore = fullSlotted.FuelAmount("wood");
        for (int i = 0; i < 100; i++) { _ = fullSlotted.IsProcessing; fullSlotted.Apply(fullSlottedState); }
        Assert(fullSlotted.InputAmount("ore") == inputBefore && fullSlotted.FuelAmount("wood") == fuelBefore && fullSlottedState.jobs.Count == 0,
            "repeated snapshots and VFX polling never consume inputs or fuel");
        foreach (float speed in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        {
            definition.processingSpeed = speed;
            Assert(!fullSlotted.IsProcessing, "invalid/nonpositive speed is idle: " + speed);
            fullSlotted.Tick(1);
        }
        definition.processingSpeed = 1;
        Assert(new PlacedBuilding().IsProcessing == false, "uninitialized visual is safely idle");
        fullSlottedState.jobs.Add(new ProcessingJob { recipe = "missing", remaining = 5 });
        Assert(!fullSlotted.IsProcessing, "missing recipe is idle");
        fullSlottedState.jobs.Clear();
        var observer = new PlacedBuilding(); observer.Initialize(definition, new BuildingState { jobs = new() { new() { recipe = "smelt", remaining = 2 } } });
        Assert(observer.IsProcessing, "late join/restored paid job is active without replaying payment");
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
    public class MonoBehaviour : Object { public Transform transform = new(); public GameObject gameObject = new(); public T GetComponent<T>() where T : class => null; }
    public class Transform { public Vector3 localScale = new(1,1,1); public void SetPositionAndRotation(Vector3 p, Quaternion q) {} public Vector3 InverseTransformPoint(Vector3 p)=>p; public Vector3 TransformPoint(Vector3 p)=>p; public T[] GetComponentsInChildren<T>(bool includeInactive)=>Array.Empty<T>(); }
    public class GameObject : Object { public Transform transform = new(); public T AddComponent<T>() where T : new() => new T(); }
    public class Mesh { public Bounds bounds; }
    public class MeshFilter { public Mesh sharedMesh; public Transform transform = new(); }
    public class SkinnedMeshRenderer { public Transform transform = new(); public Bounds localBounds; }
    public struct Bounds { public Vector3 center,size; public Bounds(Vector3 c,Vector3 s){center=c;size=s;} public Vector3 min=>center; public Vector3 max=>center; public void Encapsulate(Vector3 p){} }
    public class Texture2D : Object {}
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} public static Vector3 zero=>new(); public static Vector3 up=>new(0,1,0); public float sqrMagnitude=>x*x+y*y+z*z; public static Vector3 Scale(Vector3 a,Vector3 b)=>new(a.x*b.x,a.y*b.y,a.z*b.z); public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z); public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b); }
    public struct Quaternion { public static Quaternion Euler(float x,float y,float z) => new(); }
    public struct LayerMask { public static implicit operator LayerMask(int n)=>new(); }
    public static class Mathf { public static float Abs(float a)=>Math.Abs(a); public static float Max(float a,float b)=>Math.Max(a,b); public static float Min(float a,float b)=>Math.Min(a,b); }
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
namespace Duskborn.Effects { public class FurnaceEffects {} }
namespace InventorySystem.Data
{
    public class ItemDefinitionBase { public string Id, DisplayName, Description; public UnityEngine.Texture2D Icon; public Func<InventorySystem.Core.IInventoryItem> Factory; public InventorySystem.Core.IInventoryItem CreateRuntimeItem()=>Factory?.Invoke(); }
    public class MaterialDefinition : ItemDefinitionBase {}
}
namespace Duskborn.Gameplay.Crafting { public class RecipeDiscoveryTracker { public bool IsDiscovered(CraftingRecipe r)=>true; } }
namespace Duskborn.Gameplay.Building { public static class BuildingWorld { public static Dictionary<string,CraftingRecipe> Recipes = new(); public static CraftingRecipe Recipe(string id) => Recipes.TryGetValue(id,out var r)?r:null; } }
namespace Duskborn.Inventory
{
    public class InitialInventoryDatabase
    {
        public static InitialInventoryDatabase Instance;
        public List<Entry> BackpackItems;
        public class Entry { public InventorySystem.Data.ItemDefinitionBase Item; public int Quantity; }
    }
}
