using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using Duskborn.Gameplay.Equipment;
using Duskborn.UI;

namespace Duskborn.Gameplay.Player
{
    // Local physics on every observer; only the owner gets the camera and menu treatment.
    public sealed class PlayerDeathPresentation : MonoBehaviour
    {
        public const float OverlayDelay = 5f;
        private PlayerStats stats;
        private bool dead;
        private float elapsed;
        private Volume blur;
        private VolumeProfile profile;
        private Camera cameraView;
        private bool previousPostProcessing;
        private bool returning;
        private bool localStarted;
        private LayerMask previousVolumeMask;

        private void Awake() => stats = GetComponent<PlayerStats>();

        public void BeginDeath()
        {
            if (dead) return;
            dead = true;
            GetComponent<WeaponActionPlayer>()?.CancelAction();
            var dodge = GetComponent<PlayerDodge>();
            if (dodge != null) dodge.enabled = false;
            GetComponent<PlayerController>()?.SetInputEnabled(false);
            Disable<PlayerCombat>();

            Disable<PlayerInteractor>();
            Disable<PlayerWaterInteraction>();
            Disable<PlayerInput>();
            var controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            BuildRagdoll();
            StartLocalPresentation();
        }

        private void StartLocalPresentation()
        {
            if (localStarted || !stats.IsOwner) return;
            localStarted = true;
            Time.timeScale = 1f;
            var cameraController = GetComponent<PlayerCameraController>();
            if (cameraController != null)
            {
                cameraView = cameraController.MainCamera;
                cameraController.enabled = false; // Hold the final view while the corpse falls.
            }
            if (cameraView == null) cameraView = Camera.main;
            if (cameraView != null)
            {
                var data = cameraView.GetUniversalAdditionalCameraData();
                previousPostProcessing = data.renderPostProcessing;
                previousVolumeMask = data.volumeLayerMask;
                data.volumeLayerMask |= 1;
                data.renderPostProcessing = true;
            }
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var depth = profile.Add<DepthOfField>(true);
            depth.mode.Override(DepthOfFieldMode.Gaussian);
            depth.gaussianStart.Override(0f);
            depth.gaussianEnd.Override(.1f);
            depth.gaussianMaxRadius.Override(1.5f);
            depth.highQualitySampling.Override(true);
            var blurObject = new GameObject("Player Death Blur");
            blurObject.transform.SetParent(transform, false);
            blur = blurObject.AddComponent<Volume>();
            blur.isGlobal = true;
            blur.priority = 1000f;
            blur.sharedProfile = profile;
            blur.weight = 0f;
        }

        private void Disable<T>() where T : Behaviour
        {
            var component = GetComponent<T>();
            if (component is MonoBehaviour script) script.StopAllCoroutines();
            if (component != null) component.enabled = false;
        }

        private void Update()
        {
            if (!dead || !stats.IsOwner) return;
            StartLocalPresentation();
            elapsed += Time.unscaledDeltaTime;
            if (blur != null) blur.weight = Mathf.SmoothStep(0f, 1f, elapsed / OverlayDelay);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = elapsed >= OverlayDelay;
        }

        private void OnGUI()
        {
            if (!dead || !stats.IsOwner || elapsed < OverlayDelay || returning) return;
            GUI.depth = -1000;
            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .65f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            float width = Mathf.Min(440f, Screen.width - 32f);
            var title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 36 };
            title.normal.textColor = new Color(.9f, .8f, .65f);
            GUI.Label(new Rect((Screen.width - width) / 2, Screen.height / 2 - 90, width, 60), "YOU DIED", title);
            if (GUI.Button(new Rect((Screen.width - width) / 2, Screen.height / 2, width, 52), "Back to Main Menu"))
            {
                returning = true;
                InGameMenuController.ReturnToMainMenu();
            }
        }

        private void OnDestroy()
        {
            if (cameraView != null && profile != null)
            {
                var data = cameraView.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = previousPostProcessing;
                data.volumeLayerMask = previousVolumeMask;
            }
            if (profile != null)
            {
                foreach (var component in profile.components) if (component != null) Destroy(component);
                Destroy(profile);
            }
        }

        private void BuildRagdoll()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;
            var bodies = new Dictionary<HumanBodyBones, Rigidbody>();
            AddBone(animator, bodies, HumanBodyBones.Hips, HumanBodyBones.Spine, 8f, .16f);
            AddBone(animator, bodies, HumanBodyBones.Spine, HumanBodyBones.Neck, 6f, .16f);
            AddBone(animator, bodies, HumanBodyBones.Head, HumanBodyBones.LastBone, 3f, .12f);
            AddBone(animator, bodies, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 1.5f, .065f);
            AddBone(animator, bodies, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 1f, .055f);
            AddBone(animator, bodies, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 1.5f, .065f);
            AddBone(animator, bodies, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 1f, .055f);
            AddBone(animator, bodies, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 4f, .095f);
            AddBone(animator, bodies, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 2f, .075f);
            AddBone(animator, bodies, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 4f, .095f);
            AddBone(animator, bodies, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 2f, .075f);
            animator.enabled = false;
            var colliders = new List<Collider>();
            foreach (var body in bodies.Values)
            {
                Transform parent = body.transform.parent;
                Rigidbody connected = null;
                while (parent != null && parent != transform && connected == null)
                { connected = parent.GetComponent<Rigidbody>(); parent = parent.parent; }
                if (connected != null)
                {
                    var joint = body.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = connected;
                    joint.enablePreprocessing = false;
                    joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 25f };
                    joint.swing1Limit = new SoftJointLimit { limit = 40f };
                    joint.swing2Limit = new SoftJointLimit { limit = 25f };
                }
                colliders.Add(body.GetComponent<Collider>());
            }
            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++)
                    Physics.IgnoreCollision(colliders[i], colliders[j]);
            foreach (var body in bodies.Values)
            {
                body.isKinematic = false;
                body.AddForce(-transform.forward * .7f, ForceMode.VelocityChange);
            }
        }

        private static void AddBone(Animator animator, Dictionary<HumanBodyBones, Rigidbody> bodies,
            HumanBodyBones bone, HumanBodyBones end, float mass, float radius)
        {
            var start = animator.GetBoneTransform(bone);
            if (start == null) return;
            var finish = end == HumanBodyBones.LastBone ? null : animator.GetBoneTransform(end);
            var offset = finish != null ? start.InverseTransformPoint(finish.position) : Vector3.up * .18f;
            var capsule = start.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = offset * .5f;
            var abs = new Vector3(Mathf.Abs(offset.x), Mathf.Abs(offset.y), Mathf.Abs(offset.z));
            capsule.direction = abs.x > abs.y && abs.x > abs.z ? 0 : abs.z > abs.y ? 2 : 1;
            capsule.radius = radius;
            capsule.height = Mathf.Max(radius * 2, offset.magnitude);
            var body = start.GetComponent<Rigidbody>();
            if (body == null) body = start.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            bodies.Add(bone, body);
        }
    }
}
