using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Local channel pose and bounded core feedback; never changes gameplay or shared materials.</summary>
    internal sealed class HollowWardenChannelVisual : IDisposable
    {
        private readonly Transform _owner, _rightPlate, _leftPlate, _chest, _heart;
        private readonly Transform[] _arms = new Transform[2], _forearms = new Transform[2], _hands = new Transform[2];
        private readonly Quaternion _closedRight, _closedLeft, _openRight, _openLeft, _restChest;
        private readonly SkinnedMeshRenderer _body;
        private readonly MaterialPropertyBlock _coreProperties = new(), _effectProperties = new();
        private readonly Color _baseEmission;
        private readonly GameObject _effects;
        private readonly LineRenderer _halo;
        private readonly Transform[] _sparks = new Transform[3];
        private readonly Mesh _sparkMesh;
        private bool _glowing;

        public HollowWardenChannelVisual(Transform owner, Animator animator, Transform rightPlate,
            Transform leftPlate, Quaternion openRight, Quaternion openLeft, Material effectMaterial)
        {
            _owner = owner; _rightPlate = rightPlate; _leftPlate = leftPlate;
            _closedRight = rightPlate != null ? rightPlate.localRotation : Quaternion.identity;
            _closedLeft = leftPlate != null ? leftPlate.localRotation : Quaternion.identity;
            _openRight = openRight; _openLeft = openLeft;
            if (animator == null) return;
            foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
            {
                switch (bone.name)
                {
                    case "Chest": _chest = bone; break;
                    case "Heart": _heart = bone; break;
                    case "UpperArm.R": _arms[0] = bone; break;
                    case "UpperArm.L": _arms[1] = bone; break;
                    case "Forearm.R": _forearms[0] = bone; break;
                    case "Forearm.L": _forearms[1] = bone; break;
                    case "Hand.R": _hands[0] = bone; break;
                    case "Hand.L": _hands[1] = bone; break;
                }
            }
            _restChest = _chest != null ? _chest.localRotation : Quaternion.identity;
            _body = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            if (_body != null && _body.sharedMaterials.Length > 1)
                _baseEmission = _body.sharedMaterials[1].GetColor("_EmissionColor");
            if (_heart == null || effectMaterial == null) return;
            _effects = new GameObject("Channel core glow");
            _effects.transform.SetParent(owner, false);
            _halo = _effects.AddComponent<LineRenderer>();
            _halo.useWorldSpace = false; _halo.loop = true; _halo.positionCount = 32;
            _halo.widthMultiplier = .022f; _halo.sharedMaterial = effectMaterial;
            _halo.shadowCastingMode = ShadowCastingMode.Off; _halo.receiveShadows = false;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                _halo.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .4f);
            }
            // Three tiny faceted motes, shared geometry, no particle simulation or point lights.
            _sparkMesh = new Mesh { name = "Warden core spark" };
            _sparkMesh.vertices = new[] {Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back};
            _sparkMesh.triangles = new[] {0,4,3, 0,3,5, 0,5,2, 0,2,4, 1,3,4, 1,5,3, 1,2,5, 1,4,2};
            _sparkMesh.RecalculateBounds();
            for (int i = 0; i < _sparks.Length; i++)
            {
                var spark = new GameObject("Core ember", typeof(MeshFilter), typeof(MeshRenderer));
                _sparks[i] = spark.transform; spark.transform.SetParent(_effects.transform, false);
                spark.GetComponent<MeshFilter>().sharedMesh = _sparkMesh;
                var renderer = spark.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = effectMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off;
                _effectProperties.SetColor("_BaseColor", new Color(1, .65f, .12f, .8f));
                renderer.SetPropertyBlock(_effectProperties);
            }
            _effects.SetActive(false);
        }

        public void Apply(WardenState state, float age)
        {
            bool channel = HollowWardenEncounter.IsCoreExposed(state);
            // Override the old PhaseBreak/Recover plate tracks too: only channeling opens the chest.
            if (_rightPlate != null) _rightPlate.localRotation = channel ? _openRight : _closedRight;
            if (_leftPlate != null) _leftPlate.localRotation = channel ? _openLeft : _closedLeft;
            float pose = channel ? 1 : state == WardenState.RootPlant ? Mathf.SmoothStep(0, 1, age / 1.4f) : 0;
            float breath = Mathf.Sin(age * Mathf.PI * 2 / 2f);
            if (pose > 0 && _chest != null)
            {
                // Upright, broad casting silhouette with hands held clear of the exposed heart.
                var upright = _chest.parent.rotation * _restChest;
                _chest.rotation = Quaternion.Slerp(_chest.rotation,
                    Quaternion.AngleAxis(-5 + breath * 1.5f, _owner.right) * upright, pose);
                for (int i = 0; i < _arms.Length; i++)
                {
                    if (_arms[i] == null || _forearms[i] == null || _hands[i] == null) continue;
                    float side = Mathf.Sign(Vector3.Dot(_arms[i].position - _chest.position, _owner.right));
                    AimBone(_arms[i], _forearms[i], _owner.TransformDirection(new Vector3(side, .2f + breath * .04f, .25f)), pose);
                    AimBone(_forearms[i], _hands[i], _owner.TransformDirection(new Vector3(side * .15f, .45f + breath * .08f, .8f)), pose);
                }
            }
            SetGlow(channel, age);
        }

        private static void AimBone(Transform bone, Transform child, Vector3 direction, float weight)
        {
            var target = Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, target, weight);
        }

        private void SetGlow(bool channel, float age)
        {
            float pulse = .5f + .5f * Mathf.Sin(age * Mathf.PI * 4);
            if (_body != null && (channel || _glowing))
            {
                _body.GetPropertyBlock(_coreProperties, 1);
                _coreProperties.SetColor("_EmissionColor", channel ? new Color(1, .36f, .035f) * (3.5f + pulse * 1.5f) : _baseEmission);
                _body.SetPropertyBlock(_coreProperties, 1);
            }
            _glowing = channel;
            if (_effects == null) return;
            _effects.SetActive(channel);
            if (!channel) return;
            _effects.transform.SetPositionAndRotation(_heart.position + _owner.up * .2f + _owner.forward * .28f, _owner.rotation);
            _effects.transform.localScale = Vector3.one * (1 + pulse * .06f);
            _effectProperties.SetColor("_BaseColor", new Color(1, .55f, .08f, .35f + pulse * .25f));
            _halo.SetPropertyBlock(_effectProperties);
            for (int i = 0; i < _sparks.Length; i++)
            {
                float angle = age * 1.8f + i * Mathf.PI * 2 / _sparks.Length;
                _sparks[i].localPosition = new Vector3(Mathf.Cos(angle) * .48f, Mathf.Sin(angle) * .48f, .04f);
                _sparks[i].localScale = Vector3.one * (.025f + pulse * .008f);
            }
        }

        public void Hide() => SetGlow(false, 0);

        public void Dispose()
        {
            Hide();
            if (_effects != null) UnityEngine.Object.Destroy(_effects);
            if (_sparkMesh != null) UnityEngine.Object.Destroy(_sparkMesh);
        }
    }
}
