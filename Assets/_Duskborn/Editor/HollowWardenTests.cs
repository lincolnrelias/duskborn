using System;
using System.Linq;
using System.Reflection;
using Duskborn.Gameplay.Enemies;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    public static class HollowWardenTests
    {
        public static void RunAllTests()
        {
            HollowWardenBuilder.ValidateAssets();
            var boss=AssetDatabase.LoadAssetAtPath<GameObject>(HollowWardenBuilder.BossPath);
            var transforms=boss.GetComponentsInChildren<Transform>();
            var heart=transforms.Single(t=>t.name=="Heart");
            var hips=transforms.Single(t=>t.name=="Hips");
            Check(heart.position.z>hips.position.z,"Boss heart must face Unity +Z.");
            var skinned=boss.GetComponentInChildren<SkinnedMeshRenderer>();
            Check(skinned!=null && skinned.sharedMaterials.Length==2,"Two body materials required.");
            Check(skinned.sharedMaterials.All(m=>m!=null && m.shader!=null),"Missing body material/shader.");
            var collider=boss.GetComponent<CapsuleCollider>();
            Check(collider!=null && collider.height>3,"Boss collider scale incorrect.");
            CheckImportedScaleAndGround();
            CheckWalkingFootPlant();
            CheckSweepArc();
            CheckRootWithoutAnimator();
            CheckProductionIntegration(boss);

            var encounter=new HollowWardenEncounter();
            int impacts=0,locks=0,phaseChanges=0;
            encounter.Signal+=signal=>{
                if(signal==WardenSignal.Impact){Check(locks>impacts,"Impact before lock.");impacts++;}
                if(signal==WardenSignal.FacingLocked)locks++;
                if(signal==WardenSignal.StateChanged && encounter.State==WardenState.PhaseBreak)phaseChanges++;
            };
            encounter.Start();encounter.Tick(2);encounter.TryBeginAttack(false);
            encounter.Tick(.4);encounter.ObserveHealth(.5f);encounter.Tick(2);
            Check(impacts==1 && phaseChanges==1,"Committed hit/phase transition incorrect.");
            encounter.Tick(2);encounter.ObserveHealth(.3f);
            Check(phaseChanges==1,"Repeated phase transition.");
            for(int i=0;i<2;i++){encounter.TryBeginAttack(true);encounter.Tick(3);}
            encounter.TryBeginAttack(false);encounter.Tick(1.4);
            Check(encounter.State==WardenState.Rooted && encounter.IncomingDamageScale==1.5f,"Channel must expose the core.");
            int waves=0;
            encounter.Signal+=signal=>{if(signal==WardenSignal.SpikeWave)waves++;};
            for(int i=0;i<12;i++)encounter.Tick(.5);
            Check(waves==11,"Barrage must create twelve waves including its initial warning.");
            encounter.Tick(1.41);
            Check(encounter.State==WardenState.Recover && encounter.IncomingDamageScale==1f,"Exposure must end with channel.");
            encounter.ObserveHealth(0);encounter.Tick(100);
            Check(encounter.State==WardenState.Dead,"Death did not cancel barrage.");
            Debug.Log("[HollowWardenTests] Asset, facing, clock, root Animator regression and phase checks passed.");
        }

        private static void Check(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException(message);
        }

        private static void CheckWalkingFootPlant()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(HollowWardenBuilder.ModelFolder + "/HollowWarden.fbx");
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                var idle = AssetDatabase.LoadAllAssetsAtPath(HollowWardenBuilder.ModelFolder + "/HollowWarden.fbx")
                    .OfType<AnimationClip>().Single(c => c.name == "HW_Idle");
                idle.SampleAnimation(instance, 0);
                var type = typeof(HollowWardenBoss).Assembly.GetType("Duskborn.Gameplay.Enemies.HollowWardenLocomotion", true);
                var gait = Activator.CreateInstance(type, instance.transform, instance.GetComponent<Animator>());
                var apply = type.GetMethod("Apply");
                var restore = type.GetMethod("Restore");
                var bones = instance.GetComponentsInChildren<Transform>();
                var foot = bones.Single(t => t.name == "Foot.R");
                var hips = bones.Single(t => t.name == "Hips");
                var restPosition = hips.localPosition;
                var restRotation = hips.localRotation;
                Vector3 planted = default;
                // Reach full blend before sampling several points within the right support phase.
                for (int frame = 0; frame < 30; frame++)
                {
                    restore.Invoke(gait, null);
                    idle.SampleAnimation(instance, 0);
                    instance.transform.position += Vector3.forward * .02f;
                    apply.Invoke(gait, new object[] { true, 1f / 60 });
                    if (frame == 12) planted = foot.position;
                    if (frame > 12)
                        Check(Vector3.Distance(foot.position, planted) < .008f,
                            "Walking support foot must stay planted during straight travel.");
                }
                restore.Invoke(gait, null);
                Check(Vector3.Distance(hips.localPosition, restPosition) < .0001f &&
                    Quaternion.Angle(hips.localRotation, restRotation) < .01f,
                    "Walking offsets must restore without accumulating.");
                apply.Invoke(gait, new object[] { false, 1f / 60 });
                Check(Vector3.Distance(hips.localPosition, restPosition) < .0001f,
                    "Walking must not override attack or channel poses.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void CheckSweepArc()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(HollowWardenBuilder.ModelFolder + "/HollowWarden.fbx");
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                var clips = AssetDatabase.LoadAllAssetsAtPath(HollowWardenBuilder.ModelFolder + "/HollowWarden.fbx").OfType<AnimationClip>();
                var idle = clips.Single(c => c.name == "HW_Idle");
                var attack = clips.Single(c => c.name == "HW_HarvestSweep");
                idle.SampleAnimation(instance, 0);
                var type = typeof(HollowWardenBoss).Assembly.GetType("Duskborn.Gameplay.Enemies.HollowWardenSweepVisual", true);
                var visual = Activator.CreateInstance(type, instance.transform, instance.GetComponent<Animator>());
                var apply = type.GetMethod("Apply");
                var restore = type.GetMethod("Restore");
                var bones = instance.GetComponentsInChildren<Transform>();
                var hand = bones.Single(t => t.name == "Hand.R");
                var foot = bones.Single(t => t.name == "Foot.R");
                var chest = bones.Single(t => t.name == "Chest");
                var positions = new Vector3[3];
                float[] times = { .72f, .9f, 1.08f };
                for (int i = 0; i < times.Length; i++)
                {
                    restore.Invoke(visual, null);
                    attack.SampleAnimation(instance, times[i]);
                    var chestBefore = chest.localRotation;
                    var planted = foot.position;
                    apply.Invoke(visual, new object[] { WardenState.HarvestSweep, times[i] });
                    positions[i] = instance.transform.InverseTransformPoint(hand.position);
                    Check(Vector3.Distance(foot.position, planted) < .0001f, "Sweep must preserve planted feet.");
                    restore.Invoke(visual, null);
                    Check(Quaternion.Angle(chest.localRotation, chestBefore) < .01f, "Sweep must restore the Animator pose without accumulating.");
                }
                Check(positions[0].x - positions[2].x > 1, "Swipe fist must travel across the front, from right to left.");
                Check(positions[1].z > positions[0].z && positions[1].z > positions[2].z,
                    "Swipe must reach forward at the authoritative impact time.");
                var unchanged = hand.position;
                apply.Invoke(visual, new object[] { WardenState.Rootbreaker, .9f });
                Check(Vector3.Distance(hand.position, unchanged) < .0001f, "Swipe must not override the straight attack.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void CheckProductionIntegration(GameObject boss)
        {
            var mesh = boss.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            Check(mesh.isReadable && mesh.boneWeights.Length == mesh.vertexCount,
                "Breakup requires readable rigid skin weights in player builds.");
            Check(mesh.boneWeights.Select(w => w.boneIndex0).Distinct().Count() <= 24,
                "Boss exceeds the bounded fragment budget.");
            Check(boss.GetComponent<Duskborn.Audio.EnemyAudioFeedback>() == null,
                "Boss must not carry generic Swarmer audio.");
            var audio = Resources.Load<Duskborn.Audio.AudioDatabase>("Audio/AudioDatabase");
            Check(audio != null && audio.Player.heartbeatLoopClip != null &&
                audio.ResourcesSettings.treeFallClips.Any(c => c != null) &&
                audio.ResourcesSettings.rockShatterClips.Any(c => c != null) &&
                audio.Combat.woodHitClips.Any(c => c != null) && audio.Combat.heavySwingClips.Any(c => c != null),
                "Warden audio must resolve through serialized build-safe database references.");
            var heart = Resources.Load<InventorySystem.Data.MaterialDefinition>("Bosses/Items/material_hollow_heart");
            Check(heart != null && heart.Rarity == Duskborn.Gameplay.Loot.ItemRarity.Epic && heart.dropPrefab != null,
                "Guaranteed Hollow Heart must be an epic collectible in player builds.");
            var pickup = heart.dropPrefab.GetComponent<FishNet.Object.NetworkObject>();
            var collection = AssetDatabase.LoadAssetAtPath<FishNet.Managing.Object.SinglePrefabObjects>(HollowWardenBuilder.GameplayPrefabsPath);
            Check(pickup != null && Enumerable.Range(0, collection.GetObjectCount()).Any(i => collection.GetObject(true, i) == pickup),
                "Heart pickup must already be registered with the gameplay network collection.");
            var table = Resources.Load<Duskborn.Gameplay.Building.BuildableDefinition>("Building/Build_arcane_table");
            Check(table != null && table.costs.Count(c => c.material == heart) == 1 &&
                table.costs.Single(c => c.material == heart).amount == 1, "Arcane Table must require exactly one Hollow Heart.");
            var go = new GameObject("Warden progression wallet test");
            try
            {
                var wallet = go.AddComponent<Duskborn.Gameplay.Loot.ResourceInventory>();
                var costs = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var cost in table.costs)
                {
                    costs[cost.material.Id] = costs.TryGetValue(cost.material.Id, out int amount) ? amount + cost.amount : cost.amount;
                    if (cost.material != heart) wallet.Add(cost.material, cost.amount);
                }
                Check(!wallet.CanSpendBatch(costs), "Other materials alone must not bypass the boss progression gate.");
                wallet.Add(heart, 1);
                Check(wallet.TrySpendBatch(costs) && wallet.GetCount(heart) == 0,
                    "Building must consume the earned heart with the ordinary costs.");
                Check(!wallet.TrySpendBatch(costs), "One heart must not fund two Arcane Tables.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void CheckRootWithoutAnimator()
        {
            // Exercise the real prefab and EnemyBase callbacks without starting a server or Play Mode.
            // Keep it inactive so resetting its agent does not require a scene NavMesh.
            var holder = new GameObject("Warden root regression");
            holder.SetActive(false);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HollowWardenBuilder.RootPath);
                var instance = UnityEngine.Object.Instantiate(prefab, holder.transform);
                var root = instance.GetComponent<HollowWardenRoot>();
                Check(root != null && instance.GetComponentInChildren<Animator>(true) == null,
                    "Root must remain damageable without an Animator.");
                // FishNet's postprocessor makes Awake public in the compiled Unity assembly.
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var awake = typeof(EnemyBase).GetMethod("Awake", flags);
                var changed = typeof(EnemyBase).GetMethod("OnHPChanged", flags);
                var animatorField = typeof(EnemyBase).GetField("_animator", flags);
                // The inactive edit-mode clone never runs FishNet's runtime object setup.
                // Wire only its component owner for this fixture; do not start networking or edit the prefab.
                typeof(FishNet.Object.NetworkBehaviour).GetMethod("SerializeComponents", flags)
                    .Invoke(root, new object[] { instance.GetComponent<FishNet.Object.NetworkObject>(), (byte)0 });
                awake.Invoke(root, null);

                void ExerciseResetAndDeath()
                {
                    var collider = root.GetComponent<Collider>();
                    collider.enabled = false;
                    root.ResetEnemy(Vector3.zero);
                    Check(collider.enabled && root.CurrentHP == root.MaxHP && root.IsAlive,
                        "Static root reset must restore its collider and health.");
                    int notifications = 0;
                    Action<float, float> onHealth = (hp, max) => { if (hp == 0) notifications++; };
                    root.OnHealthChanged += onHealth;
                    try
                    {
                        changed.Invoke(root, new object[] { root.MaxHP, 0f, true });
                        changed.Invoke(root, new object[] { root.MaxHP, 0f, false });
                        Check(notifications == 2, "Root death health callbacks must complete on server and client.");
                    }
                    finally { root.OnHealthChanged -= onHealth; }
                }

                ExerciseResetAndDeath();
                // A destroyed Unity object still has a CLR reference: catch null-conditional access regressions.
                var missingAnimator = instance.AddComponent<Animator>();
                animatorField.SetValue(root, missingAnimator);
                UnityEngine.Object.DestroyImmediate(missingAnimator);
                ExerciseResetAndDeath();
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }

        private static void CheckImportedScaleAndGround()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(HollowWardenBuilder.ModelFolder+"/HollowWarden.fbx");
            var instance=UnityEngine.Object.Instantiate(source);
            var baked=new Mesh();
            try
            {
                var idle=AssetDatabase.LoadAllAssetsAtPath(HollowWardenBuilder.ModelFolder+"/HollowWarden.fbx")
                    .OfType<AnimationClip>().Single(c=>c.name=="HW_Idle");
                idle.SampleAnimation(instance,0);
                var renderer=instance.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.BakeMesh(baked);
                float low=float.PositiveInfinity,high=float.NegativeInfinity;
                foreach(var vertex in baked.vertices)
                {
                    float y=renderer.transform.TransformPoint(vertex).y;
                    low=Mathf.Min(low,y);high=Mathf.Max(high,y);
                }
                Check(Mathf.Abs(low)<.005f,"Imported rest soles must touch ground; min Y="+low);
                Check(high-low>3.8f && high-low<4.3f,"Imported boss must remain about 4 m tall; height="+(high-low));
                Debug.Log($"[HollowWardenTests] Imported rest mesh height={high-low:F3} m, sole Y={low:F4} m.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}

