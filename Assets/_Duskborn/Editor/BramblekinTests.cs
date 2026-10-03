using System.Linq;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Loot;
using FishNet.Managing.Object;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Duskborn.Editor
{
    public static class BramblekinTests
    {
        [System.Serializable] private class PoseSample { public string name; public float[] delta; }
        [System.Serializable] private class PoseFrame { public PoseSample[] parts; }
        [System.Serializable] private class PoseSequence { public PoseFrame[] frames; }
        public static void RunAllTests()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BramblekinBuilder.PrefabPath);
            BramblekinBuilder.Require(prefab != null && prefab.GetComponent<Bramblekin>() != null, "Basic spawn prefab missing.");
            // Builders are opt-in; pre-install compilation/tests still exercise the clock.
            Clock();
            if (prefab.GetComponent<BramblekinPresentation>() == null) return;
            BramblekinBuilder.Require(System.IO.File.ReadAllText(BramblekinBuilder.PrefabPath).Contains("m_Name: Bramblekin"), "Basic enemy root name.");
            var agent = prefab.GetComponent<NavMeshAgent>();
            BramblekinBuilder.Require(agent.radius <= .35f && agent.stoppingDistance < BramblekinClock.EngageRange, "Compact navigable melee footprint.");
            var stats = new SerializedObject(prefab.GetComponent<Bramblekin>());
            BramblekinBuilder.Require(stats.FindProperty("playerLayer").intValue == 1 << LayerMask.NameToLayer("Player"), "Player hit mask.");
            BramblekinBuilder.Require(stats.FindProperty("creatureTypes").intValue == (int)Duskborn.Gameplay.TargetType.Humanoid &&
                stats.FindProperty("_damageNumberConfig").objectReferenceValue != null && stats.FindProperty("outlineLayerName").stringValue == "EnemyOutline", "Targeting/damage feedback.");
            BramblekinBuilder.Require(stats.FindProperty("_networkObjectCache").objectReferenceValue == prefab.GetComponent<NetworkObject>() &&
                stats.FindProperty("_componentIndexCache").intValue < 250, "FishNet behaviour association.");
            BramblekinBuilder.Require(!new SerializedObject(prefab.GetComponent<FishNet.Component.Transforming.NetworkTransform>())
                .FindProperty("_clientAuthoritative").boolValue, "Movement must remain server authoritative.");
            BramblekinBuilder.Require(prefab.GetComponent<NetworkObject>().NetworkBehaviours.All(b => b != null) &&
                prefab.GetComponent<NetworkObject>().NetworkBehaviours.Contains(prefab.GetComponent<Bramblekin>()), "FishNet behaviour list.");
            BramblekinBuilder.Require(stats.FindProperty("weapon").objectReferenceValue == null, "Legacy weapon must be removed.");
            BramblekinBuilder.Require(stats.FindProperty("deathDelay").floatValue == 10, "Preserve ordinary corpse lifetime.");
            BramblekinBuilder.Require(prefab.GetComponent<LootDropper>()?.LootTable != null, "Existing loot missing.");
            foreach (var entry in prefab.GetComponent<LootDropper>().LootTable.entries)
                BramblekinBuilder.Require(entry.itemDefinition != null && entry.itemDefinition.dropPrefab != null, "Canonical loot pickup missing.");
            var presentation = new SerializedObject(prefab.GetComponent<BramblekinPresentation>());
            foreach (string cue in BramblekinBuilder.Cues)
            {
                var clips = presentation.FindProperty(char.ToLowerInvariant(cue[0]) + cue.Substring(1) + "Clips");
                BramblekinBuilder.Require(clips.arraySize == BramblekinBuilder.Takes(cue), "Missing cue variants.");
                for (int i = 0; i < clips.arraySize; i++)
                    BramblekinBuilder.Require(clips.GetArrayElementAtIndex(i).objectReferenceValue != null, "Null audio cue.");
            }
            var parts = prefab.GetComponentsInChildren<MeshFilter>();
            BramblekinBuilder.Require(parts.Length == 10 && parts.All(p => p.sharedMesh != null && p.sharedMesh.uv.Length == p.sharedMesh.vertexCount), "Ten articulated UV-mapped parts.");
            BramblekinBuilder.Require(prefab.GetComponentsInChildren<CharacterJoint>().Length == 9, "Articulated corpse graph.");
            BramblekinBuilder.Require(agent.speed == 6.2f && stats.FindProperty("_entity.moveSpeed").floatValue == 6.2f, "Faster movement.");
            foreach (string path in new[] { "Assets/DefaultPrefabObjects.asset", "Assets/_Duskborn/Network/DefaultPrefabObjects.asset" })
            {
                var objects = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(path);
                BramblekinBuilder.Require(objects != null && Enumerable.Range(0, objects.GetObjectCount()).Any(i =>
                    objects.GetObject(true, i) == prefab.GetComponent<NetworkObject>()), "FishNet registration.");
            }
            var registry = AssetDatabase.LoadAssetAtPath<EnemyPrefabRegistry>("Assets/_Duskborn/ScriptableObjects/Enemies/EnemyPrefabRegistry.asset");
            BramblekinBuilder.Require(registry.Entries.Single(e => e.Type == EnemyType.Swarmer).Prefab == prefab.GetComponent<Bramblekin>(), "Basic registry GUID/reference preserved.");
            BramblekinBuilder.Require(Bramblekin.InSwing(new Vector3(0,0,3.3f),Vector3.forward) &&
                Bramblekin.InSwing(new Vector3(.15f,0,1.35f),Vector3.forward) &&
                !Bramblekin.InSwing(new Vector3(0,0,3.31f),Vector3.forward) &&
                !Bramblekin.InSwing(new Vector3(0,0,-1),Vector3.forward) && !Bramblekin.InSwing(new Vector3(0,2,1),Vector3.forward), "Melee reach/facing/elevation.");
            BramblekinBuilder.Require(stats.FindProperty("_entity.attackSpeed").floatValue == 2 &&
                Mathf.Abs(BramblekinClock.WindupSeconds + BramblekinClock.SwingSeconds + BramblekinClock.RecoverSeconds - .74f) < .0001f,
                "Double attack cadence and stat agreement.");
            var approach = new BramblekinClock(); approach.Reset();
            BramblekinBuilder.Require(!approach.TryBegin(3.3f) && !approach.TryBegin(1.21f) && approach.TryBegin(1.2f),
                "Approach closely instead of swinging at outer reach.");
            GroundedParts(prefab);
            KillingImpact(prefab);
            SpawnBudgets();
            Debug.Log("[Bramblekin] Asset, combat and registration checks passed.");
        }
        private static void Clock()
        {
            var clock = new BramblekinClock(); clock.Reset();
            BramblekinBuilder.Require(clock.TryBegin(1), "Start basic swing."); clock.Tick(100);
            BramblekinBuilder.Require(clock.Phase == BramblekinPhase.Swing && clock.Age == 0, "Hitch must preserve swing."); clock.Tick(100);
            BramblekinBuilder.Require(clock.Phase == BramblekinPhase.Recover && clock.Age == 0 && !clock.TryBegin(1), "Hitch must preserve recovery.");
            clock.Kill(); clock.Tick(100); BramblekinBuilder.Require(clock.Phase == BramblekinPhase.Dead, "Corpse attack guard.");
            clock.Reset(); BramblekinBuilder.Require(clock.TryBegin(1), "Reused attack reset.");
            clock.Interrupt(); BramblekinBuilder.Require(clock.Phase == BramblekinPhase.Recover, "Stagger cancels commitment.");
        }
        private static void GroundedParts(GameObject prefab)
        {
            var root = Object.Instantiate(prefab);
            try
            {
                root.GetComponent<NavMeshAgent>().enabled = false;
                var presentation = root.GetComponent<BramblekinPresentation>();
                var initialize = typeof(BramblekinPresentation).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                initialize.Invoke(presentation, null);
                var pose = typeof(BramblekinPresentation).GetMethod("ApplyPose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var meshes = root.GetComponentsInChildren<MeshFilter>();
                var restMatrices = meshes.Select(m => m.transform.localToWorldMatrix).ToArray();
                var poseFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var capturePose = typeof(BramblekinPresentation).GetMethod("CaptureUpperBody", poseFlags);
                var smoothPose = typeof(BramblekinPresentation).GetMethod("SmoothUpperBody", poseFlags);
                float gaitBlend = 1 - Mathf.Exp(-26 * (1.6f / 6.2f / 16));
                for (int frame = 0; frame < 32; frame++)
                {
                    capturePose.Invoke(presentation, null);
                    pose.Invoke(presentation, new object[] { BramblekinPhase.Hunt, 0f, 1f, frame * Mathf.PI * 2 / 16 });
                    smoothPose.Invoke(presentation, new object[] { gaitBlend });
                }
                var sequence = new PoseSequence { frames = new PoseFrame[16] };
                for (int frame = 0; frame < sequence.frames.Length; frame++)
                {
                    capturePose.Invoke(presentation, null);
                    pose.Invoke(presentation, new object[] { BramblekinPhase.Hunt, 0f, 1f, frame * Mathf.PI * 2 / sequence.frames.Length });
                    smoothPose.Invoke(presentation, new object[] { gaitBlend });
                    var samples = new PoseSample[meshes.Length];
                    for (int index = 0; index < meshes.Length; index++)
                    {
                        var delta = meshes[index].transform.localToWorldMatrix * restMatrices[index].inverse;
                        var numbers = new float[16];
                        for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++) numbers[row * 4 + column] = delta[row, column];
                        samples[index] = new PoseSample { name = meshes[index].name, delta = numbers };
                    }
                    sequence.frames[frame] = new PoseFrame { parts = samples };
                }
                System.IO.Directory.CreateDirectory("Logs/UnityCli");
                System.IO.File.WriteAllText("Logs/UnityCli/bramblekin-gait-poses.json", JsonUtility.ToJson(sequence));
                var attackSequence = new PoseSequence { frames = new PoseFrame[46] };
                pose.Invoke(presentation, new object[] { BramblekinPhase.Hunt, 0f, 0f, 0f });
                for (int frame = 0; frame < attackSequence.frames.Length; frame++)
                {
                    float time = frame / 60f;
                    var phase = time < BramblekinClock.WindupSeconds ? BramblekinPhase.Windup :
                        time < BramblekinClock.WindupSeconds + BramblekinClock.SwingSeconds ? BramblekinPhase.Swing : BramblekinPhase.Recover;
                    float age = time - (phase == BramblekinPhase.Windup ? 0 : phase == BramblekinPhase.Swing ?
                        BramblekinClock.WindupSeconds : BramblekinClock.WindupSeconds + BramblekinClock.SwingSeconds);
                    capturePose.Invoke(presentation, null);
                    pose.Invoke(presentation, new object[] { phase, age, 0f, 0f });
                    smoothPose.Invoke(presentation, new object[] { 1 - Mathf.Exp(-(phase == BramblekinPhase.Swing ? 45 : 26) * BramblekinClock.AttackPlaybackSpeed / 60f) });
                    var samples = new PoseSample[meshes.Length];
                    for (int index = 0; index < meshes.Length; index++)
                    {
                        var delta = meshes[index].transform.localToWorldMatrix * restMatrices[index].inverse;
                        var numbers = new float[16];
                        for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++) numbers[row * 4 + column] = delta[row, column];
                        samples[index] = new PoseSample { name = meshes[index].name, delta = numbers };
                    }
                    attackSequence.frames[frame] = new PoseFrame { parts = samples };
                }
                System.IO.File.WriteAllText("Logs/UnityCli/bramblekin-attack-poses.json", JsonUtility.ToJson(attackSequence));
                // Shared endpoints prevent visible snaps at normal replicated phase changes.
                var joints = root.GetComponentsInChildren<Transform>().Where(t => t != root.transform).ToArray();
                foreach (var transition in new[] {
                    (BramblekinPhase.Hunt, 0f, BramblekinPhase.Windup, 0f),
                    (BramblekinPhase.Windup, BramblekinClock.WindupSeconds, BramblekinPhase.Swing, 0f),
                    (BramblekinPhase.Swing, BramblekinClock.SwingSeconds, BramblekinPhase.Recover, 0f),
                    (BramblekinPhase.Recover, BramblekinClock.RecoverSeconds, BramblekinPhase.Hunt, 0f) })
                {
                    pose.Invoke(presentation, new object[] { transition.Item1, transition.Item2, 1f, .7f });
                    var beforePositions = joints.Select(t => t.position).ToArray();
                    var beforeRotations = joints.Select(t => t.rotation).ToArray();
                    pose.Invoke(presentation, new object[] { transition.Item3, transition.Item4, 1f, .7f });
                    for (int i = 0; i < joints.Length; i++)
                        BramblekinBuilder.Require(Vector3.Distance(beforePositions[i], joints[i].position) < .0001f &&
                            Quaternion.Angle(beforeRotations[i], joints[i].rotation) < .1f, "Animation phase boundary snap: " + transition.Item3);
                }
                // Late observers and interrupted windups must blend from the displayed pose.
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var capture = typeof(BramblekinPresentation).GetMethod("CaptureUpperBody", flags);
                var smooth = typeof(BramblekinPresentation).GetMethod("SmoothUpperBody", flags);
                pose.Invoke(presentation, new object[] { BramblekinPhase.Windup, .15f, 0f, 0f });
                var arm = joints.Single(t => t.name == "Arm.R");
                var start = arm.rotation;
                capture.Invoke(presentation, null);
                pose.Invoke(presentation, new object[] { BramblekinPhase.Recover, 0f, 0f, 0f });
                var target = arm.rotation;
                smooth.Invoke(presentation, new object[] { 1 - Mathf.Exp(-26f / 60) });
                BramblekinBuilder.Require(Quaternion.Angle(start, arm.rotation) < Quaternion.Angle(start, target) * .6f &&
                    Quaternion.Angle(start, arm.rotation) > 1, "Interrupted attack must ease rather than snap or freeze.");
                var clubArm = meshes.Single(m => m.name == "Arm.R");
                var clubVertices = clubArm.sharedMesh.vertices.Where((v, i) => clubArm.sharedMesh.uv[i].x >= .3f).ToArray();
                float clubLowest = float.PositiveInfinity;
                foreach (var phase in new[] { BramblekinPhase.Windup, BramblekinPhase.Swing, BramblekinPhase.Recover })
                    for (int sample = 0; sample <= 60; sample++)
                    {
                        float duration = phase == BramblekinPhase.Windup ? BramblekinClock.WindupSeconds :
                            phase == BramblekinPhase.Swing ? BramblekinClock.SwingSeconds : BramblekinClock.RecoverSeconds;
                        pose.Invoke(presentation, new object[] { phase, duration * sample / 60, 0f, 0f });
                        clubLowest = Mathf.Min(clubLowest, clubVertices.Min(v => clubArm.transform.TransformPoint(v).y));
                    }
                BramblekinBuilder.Require(clubLowest >= -.015f, "Larger club crosses ground during follow-through: " + clubLowest);
                Debug.Log("[Bramblekin] Larger club sampled ground clearance: " + clubLowest);
                float lowest = float.PositiveInfinity;
                foreach (BramblekinPhase phase in new[] { BramblekinPhase.Hunt, BramblekinPhase.Windup, BramblekinPhase.Swing, BramblekinPhase.Recover })
                    for (int sample = 0; sample <= 60; sample++)
                    {
                        pose.Invoke(presentation, new object[] { phase, sample / 60f, 1f, sample / 60f * Mathf.PI * 2 });
                        foreach (var foot in root.GetComponentsInChildren<MeshFilter>().Where(p => p.name.StartsWith("Foot.")))
                            lowest = Mathf.Min(lowest, foot.sharedMesh.vertices.Min(v => foot.transform.TransformPoint(v).y));
                    }
                BramblekinBuilder.Require(lowest >= -.015f, "Imported animated feet penetrate sole plane: " + lowest);
                pose.Invoke(presentation, new object[] { BramblekinPhase.Recover, 1f, 0f, 0f });
                foreach (var foot in root.GetComponentsInChildren<MeshFilter>().Where(p => p.name.StartsWith("Foot.")))
                {
                    float minimum = foot.sharedMesh.vertices.Min(v => foot.transform.TransformPoint(v).y);
                    BramblekinBuilder.Require(minimum >= -.015f && minimum <= .02f, "Imported foot sole plane: " + minimum);
                }
                var ragdoll = root.GetComponent<EnemyRagdoll>(); ragdoll.EnsureInitialized();
                ragdoll.EnableRagdoll(); BramblekinBuilder.Require(ragdoll.IsRagdoll, "Death handover.");
                ragdoll.DisableRagdoll(); BramblekinBuilder.Require(!ragdoll.IsRagdoll, "Reuse handover.");
                BramblekinBuilder.Require(root.GetComponentsInChildren<Rigidbody>().Where(b => b.gameObject != root).All(b => b.isKinematic), "Reuse bodies.");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void SpawnBudgets()
        {
            int scenarios = 0, basicCount = 0;
            for (int night = 1; night <= 6; night++)
            {
                var def = AssetDatabase.LoadAssetAtPath<NightDefinition>("Assets/_Duskborn/ScriptableObjects/Enemies/Night_" + night + "_Definition.asset");
                BramblekinBuilder.Require(def != null, "Night definition missing.");
                BramblekinBuilder.Require(def.Pools.SelectMany(p => p.Entries).Any(e => e.Type == EnemyType.Swarmer && e.Cost == 6), "Basic budget changed.");
                foreach (int players in new[] { 1, 5 }) for (int seed = 0; seed < 20; seed++)
                {
                    var timeline = TimelineGenerator.Generate(def, players, 120, new Duskborn.Core.SeededRNG(seed));
                    BramblekinBuilder.Require(timeline.TotalBudgetSpent <= def.BaseBudget * (1 + (players - 1) * .4f), "Spawn budget overflow.");
                    basicCount += timeline.Events.Count(e => e.EnemyType == EnemyType.Swarmer); scenarios++;
                }
            }
            BramblekinBuilder.Require(basicCount > 0, "Basic enemy never selected.");
            Debug.Log($"[Bramblekin] {scenarios} seeded wave scenarios, {basicCount} basic spawns, budgets bounded.");
        }
        private static void KillingImpact(GameObject prefab)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = Object.Instantiate(prefab);
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<NavMeshAgent>().enabled = false;
                var physics = scene.GetPhysicsScene();
                BramblekinBuilder.Require(physics != Physics.defaultPhysicsScene, "Impact test must be isolated.");
                var ragdoll = root.GetComponent<EnemyRagdoll>(); ragdoll.EnsureInitialized();
                var bodies = root.GetComponentsInChildren<Rigidbody>().Where(b => b.gameObject != root).ToArray();
                var torso = bodies.Single(b => b.name == "Body");
                foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.forward })
                {
                    ragdoll.DisableRagdoll(); root.transform.position = Vector3.up * 3;
                    Physics.SyncTransforms();
                    Vector3 contact = torso.worldCenterOfMass + Vector3.up * .18f;
                    ragdoll.ApplyImpulse(contact, direction);
                    // Duplicate delivery must not double the killing impulse.
                    ragdoll.ApplyImpulse(contact, direction);
                    physics.Simulate(.02f);
                    float momentum = bodies.Sum(b => Vector3.Dot(b.linearVelocity, direction) * b.mass);
                    BramblekinBuilder.Require(momentum > 18 && momentum < 25, "Killing-hit directional momentum/once-only guard: " + momentum);
                    BramblekinBuilder.Require(bodies.Any(b => b.angularVelocity.sqrMagnitude > .01f), "Off-center killing contact must turn corpse.");
                }
                ragdoll.DisableRagdoll();
                BramblekinBuilder.Require(bodies.All(b => b.isKinematic), "Death impulse reuse reset.");
                Debug.Log("[Bramblekin] Isolated killing-hit physics: right/left/forward momentum, contact torque, duplicate guard and reuse passed.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
