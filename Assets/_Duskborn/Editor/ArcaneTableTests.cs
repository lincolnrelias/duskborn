using System;
using System.Reflection;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using InventorySystem.Core;
using InventorySystem.Data;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    public static class ArcaneTableTests
    {
        [MenuItem("Duskborn/Tests/Run Arcane Table Tests")]
        public static void RunAllTests()
        {
            TestCatalog(); TestScaling(); TestEtchingTransaction(); TestRemovalRefund(); TestReentrantEtching(); TestUI(); TestRuneIdentifiers(); TestParticleVelocityModes();
            Debug.Log("[ArcaneTableTests] Eight arcane suites passed.");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static WeaponItem Weapon() => new WeaponItem("test_sword", "Sword", "", "", null, null, null, null, null);
        private static WeaponEtching Rune(RuneKind kind, int level) => new WeaponEtching { kind = kind, level = level };
        private static void TestCatalog()
        {
            int count = 0;
            foreach (RuneKind kind in Enum.GetValues(typeof(RuneKind)))
            {
                if (kind == RuneKind.None) continue;
                for (int level = 1; level <= 3; level++)
                {
                    string id = RuneCatalog.Id(kind, level);
                    var stone = Resources.Load<MaterialDefinition>("Runestones/" + id);
                    var recipe = Resources.Load<CraftingRecipe>("Crafting/Recipe_" + id);
                    Check(stone != null && stone.Icon != null && stone.dropPrefab != null, id + " must have an imported sprite texture and world drop");
                    Check(recipe != null && recipe.OutputItem == stone && recipe.OutputAmount == 1 &&
                        recipe.RequiredStation == CraftingStationType.ArcaneTable && recipe.ProcessingSeconds == 0,
                        id + " recipe is not wired to the arcane table");
                    bool gathered = false, dropped = false;
                    foreach (var ingredient in recipe.Ingredients)
                    {
                        Check(ingredient.material != null && ingredient.amount > 0, "Missing rune ingredient");
                        gathered |= ingredient.material.Id == "material_stone";
                        dropped |= ingredient.material.Id == "material_leather" || ingredient.material.Id == "material_bone";
                    }
                    Check(gathered && dropped, "Runes require gathering and enemy loot");
                    count++;
                }
            }
            Check(count == 24, "Expected 24 rune items");
        }
        private static void TestScaling()
        {
            for (int kind = 1; kind <= 8; kind++) for (int level = 1; level <= 3; level++)
            {
                var rune = Rune((RuneKind)kind, level);
                Check(WeaponEtching.Unpack(rune.Packed).Packed == rune.Packed, "Rune roundtrip");
                Check(RuneCatalog.AddStacks(0, rune) == level, "Rune tier must equal stacks per hit");
                Check(RuneCatalog.AddStacks(11, rune) == 12, "Stack cap");
            }
            Check(!WeaponEtching.Unpack(255).IsValid && !Rune(RuneKind.Flame, 4).IsValid, "Reject invalid network rune payloads");
            Check(RuneCatalog.TickDamage(RuneKind.Flame, 3) == 6 && RuneCatalog.TickDamage(RuneKind.Venom, 3) == 4.5f,
                "DOT must scale with stacks");
            Check(RuneCatalog.TickDamage(RuneKind.Stone, 12) == 0, "Utility runes must not inflict accidental DOT");
            Check(!RuneCatalog.ShouldProc(RuneKind.Storm, 5) && RuneCatalog.ShouldProc(RuneKind.Storm, 6), "Storm threshold must be 6 stacks");
            Check(!RuneCatalog.ShouldProc(RuneKind.Frost, 7) && RuneCatalog.ShouldProc(RuneKind.Frost, 8), "Freeze threshold must be 8 stacks");
        }
        private static void TestEtchingTransaction()
        {
            var go = new GameObject("Rune wallet test");
            try
            {
                var wallet = go.AddComponent<ResourceInventory>();
                var inv = new InventoryService(new InventoryGrid(2, 1));
                var first = Weapon(); var second = Weapon(); inv.TryAddItem(first, out _); inv.TryAddItem(second, out _);
                wallet.Add(RuneCatalog.Id(RuneKind.Flame, 2), 2);
                Check(RuneEtchingService.TryEtch(wallet, inv, null, first, Rune(RuneKind.Flame, 2)), "Etch held instance");
                Check(first.Etching.level == 2 && !second.Etching.IsValid, "Must not modify same-ID sibling weapons");
                Check(wallet.GetCount(RuneCatalog.Id(RuneKind.Flame, 2)) == 1, "Consume exactly one rune");
                Check(!RuneEtchingService.TryEtch(wallet, inv, null, first, Rune(RuneKind.Flame, 1)), "No downgraded same-family etch");
                Check(!RuneEtchingService.TryEtch(wallet, inv, null, first, Rune(RuneKind.Venom, 3)), "No free rune etch");
                wallet.Add(RuneCatalog.Id(RuneKind.Venom, 1), 1);
                Check(RuneEtchingService.TryEtch(wallet, inv, null, first, Rune(RuneKind.Venom, 1)) && first.Etching.kind == RuneKind.Venom,
                    "Cross-family replacement");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        private static void TestRemovalRefund()
        {
            var go = new GameObject("Rune stale target test");
            try
            {
                var wallet = go.AddComponent<ResourceInventory>(); var inv = new InventoryService(new InventoryGrid(1, 1));
                var weapon = Weapon(); inv.TryAddItem(weapon, out int index);
                string id = RuneCatalog.Id(RuneKind.Stone, 1); wallet.Add(id, 1);
                wallet.ResourceChanged += (_, count) => { if (count == 0) inv.RemoveItem(index); };
                Check(!RuneEtchingService.TryEtch(wallet, inv, null, weapon, Rune(RuneKind.Stone, 1)), "Removed target must reject");
                Check(wallet.GetCount(id) == 1 && !weapon.Etching.IsValid, "Removed target must refund rune");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        private static void TestReentrantEtching()
        {
            var go = new GameObject("Rune callback test");
            try
            {
                var wallet = go.AddComponent<ResourceInventory>(); var inv = new InventoryService(new InventoryGrid(1, 1));
                var weapon = Weapon(); inv.TryAddItem(weapon, out _); string id = RuneCatalog.Id(RuneKind.Flame, 1); wallet.Add(id, 3);
                bool nested = true;
                wallet.ResourceChanged += (_, __) => nested = RuneEtchingService.TryEtch(wallet, inv, null, weapon, Rune(RuneKind.Flame, 1));
                Check(RuneEtchingService.TryEtch(wallet, inv, null, weapon, Rune(RuneKind.Flame, 1)), "Outer etch must succeed");
                Check(!nested && wallet.GetCount(id) == 2, "Callbacks must not consume additional runes");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        // Exercise the native particle setup and reconfiguration on the same aura,
        // which asset-reference and arithmetic tests cannot validate.
        public static void TestParticleVelocityModes()
        {
            var go = new GameObject("Rune velocity regression");
            try
            {
                var aura = go.AddComponent<Duskborn.Effects.RuneAura>();
                foreach (RuneKind kind in Enum.GetValues(typeof(RuneKind)))
                {
                    if (kind == RuneKind.None) continue;
                    aura.Configure(kind, 3, new Bounds(Vector3.zero, Vector3.one));
                    var particles = go.GetComponentInChildren<ParticleSystem>();
                    Check(particles != null, kind + " must create particles");
                    var velocity = particles.velocityOverLifetime;
                    Check(velocity.enabled && velocity.x.mode == ParticleSystemCurveMode.TwoConstants &&
                        velocity.y.mode == velocity.x.mode && velocity.z.mode == velocity.x.mode,
                        kind + ": particle velocity curves must share TwoConstants mode");
                    float expected = kind == RuneKind.Blood || kind == RuneKind.Stone ? -.4f : kind == RuneKind.Flame ? .6f : .1f;
                    Check(Mathf.Approximately(velocity.y.constantMin, expected) &&
                        Mathf.Approximately(velocity.y.constantMax, expected), kind + ": preserve vertical velocity");
                    particles.Simulate(.25f, true, true);
                }
                aura.Configure(RuneKind.None, 0, new Bounds());
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Debug.Log("[ArcaneTableTests] Particle velocity regression passed for all eight rune families.");
        }

        private static void TestRuneIdentifiers()
        {
            Check(RuneCatalog.TryParse("runestone_flame_3", out var rune) && rune.kind == RuneKind.Flame && rune.level == 3, "World rune identity");
            Check(!RuneCatalog.TryParse("runestone_flame_4", out _) && !RuneCatalog.TryParse("runestone_9_1", out _) &&
                !RuneCatalog.TryParse("runestone_flame_01", out _) && !RuneCatalog.TryParse("../runestone_flame_1", out _), "Reject malformed rune identities");
            Check(Resources.Load<Shader>("Shaders/RuneAura") != null, "Rune aura shader must be included in builds");
        }
        private static void TestUI()
        {
            var go = new GameObject("Arcane UI test");
            try
            {
                var ui = go.AddComponent<Duskborn.UI.ArcaneTableUI>(); ui.Show();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var rows = (System.Collections.ICollection)ui.GetType().GetField("_catalogRows", flags).GetValue(ui);
                Check(rows.Count == 24, "Forge view must list all 24 rune recipes");
                var root = (RectTransform)ui.GetType().GetField("_root", flags).GetValue(ui);
                var canvas = root.GetComponentInParent<UnityEngine.Canvas>();
                Check(canvas != null && canvas.sortingOrder == 130 && root.gameObject.activeSelf, "Dedicated arcane canvas");
                var view = ui.GetType().GetNestedType("View", BindingFlags.NonPublic);
                ui.GetType().GetMethod("SetView", flags).Invoke(ui, new[] { Enum.Parse(view, "Etch") });
                Check(rows.Count == 24, "Etch view must list all rune identities");
                var action = (UnityEngine.UI.Button)ui.GetType().GetField("_action", flags).GetValue(ui);
                Check(!action.interactable, "Etching is disabled without a nearby player, rune and weapon");
                ui.Hide(); Check(!root.gameObject.activeSelf, "Close hides arcane UI");
                ui.Show(); Check(ReferenceEquals(root, ui.GetType().GetField("_root", flags).GetValue(ui)), "Reopening must reuse the canvas");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
