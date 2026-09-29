using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Clock-driven low fist sweep, independent of the imported rig's local bone axes.</summary>
    internal sealed class HollowWardenSweepVisual
    {
        private readonly Transform _owner, _chest, _arm, _elbow, _hand;
        private readonly List<Bone> _bones = new();
        private readonly float _upperLength, _lowerLength;
        private bool _applied;

        private sealed class Bone
        {
            public Transform Transform;
            public Quaternion Rest, Animated;
        }

        public HollowWardenSweepVisual(Transform owner, Animator animator)
        {
            _owner = owner;
            if (animator == null) return;
            foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
            {
                switch (bone.name)
                {
                    case "Chest": _chest = bone; break;
                    case "UpperArm.R": _arm = bone; break;
                    case "Forearm.R": _elbow = bone; break;
                    case "Hand.R": _hand = bone; break;
                    default: continue;
                }
                _bones.Add(new Bone { Transform = bone, Rest = bone.localRotation });
            }
            if (_arm == null || _elbow == null || _hand == null) return;
            _upperLength = Vector3.Distance(_arm.position, _elbow.position);
            _lowerLength = Vector3.Distance(_elbow.position, _hand.position);
        }

        public void Restore()
        {
            if (!_applied) return;
            foreach (var bone in _bones) bone.Transform.localRotation = bone.Animated;
            _applied = false;
        }

        public void Apply(WardenState state, float clipAge)
        {
            if (state != WardenState.HarvestSweep || _chest == null || _hand == null || _arm == null || _elbow == null) return;
            float weight = Mathf.SmoothStep(0, 1, clipAge / .2f) *
                (1 - Mathf.SmoothStep(0, 1, (clipAge - 1.35f) / .85f));
            if (weight <= 0) return;
            foreach (var bone in _bones)
            {
                bone.Animated = bone.Transform.localRotation;
                bone.Transform.localRotation = bone.Rest;
            }
            _applied = true;
            float windup = Mathf.SmoothStep(0, 1, clipAge / .6f);
            // Hold the loaded shoulder until facing locks; cross the front at the .9 s hit.
            float sweep = Mathf.SmoothStep(0, 1, (clipAge - .72f) / .36f);
            _chest.rotation = Quaternion.AngleAxis(Mathf.Lerp(32 * windup, -42, sweep), _owner.up) *
                Quaternion.AngleAxis(Mathf.Lerp(3, 16, sweep), _owner.right) * _chest.rotation;

            float angle = Mathf.Lerp(78, -72, sweep) * Mathf.Deg2Rad;
            Vector3 target = _owner.TransformPoint(new Vector3(Mathf.Sin(angle) * 1.95f,
                Mathf.Lerp(1.3f, .78f, windup), Mathf.Cos(angle) * 1.95f));
            Vector3 delta = target - _arm.position;
            float length = Mathf.Clamp(delta.magnitude, Mathf.Abs(_upperLength - _lowerLength) + .001f,
                _upperLength + _lowerLength - .001f);
            Vector3 axis = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(_owner.right + _owner.up * .35f, axis).normalized;
            float along = (_upperLength * _upperLength - _lowerLength * _lowerLength + length * length) / (2 * length);
            Vector3 elbow = _arm.position + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, _upperLength * _upperLength - along * along));
            Quaternion handRotation = _hand.rotation;
            _arm.rotation = Quaternion.FromToRotation(_elbow.position - _arm.position, elbow - _arm.position) * _arm.rotation;
            _elbow.rotation = Quaternion.FromToRotation(_hand.position - _elbow.position,
                _arm.position + axis * length - _elbow.position) * _elbow.rotation;
            // Keep the long stone fist pointing down so its lower edge scrapes the ground.
            _hand.rotation = handRotation;
            foreach (var bone in _bones)
                bone.Transform.localRotation = Quaternion.Slerp(bone.Animated, bone.Transform.localRotation, weight);
        }
    }
}
