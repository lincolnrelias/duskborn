using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Distance-driven local presentation for the Warden's generic, segmented rig.</summary>
    internal sealed class HollowWardenLocomotion
    {
        private const float Stance = .6f;
        private const float StepTravel = .95f;
        private const float CycleDistance = StepTravel / Stance;
        private readonly Transform _owner;
        private readonly Dictionary<string, Bone> _bones = new();
        private readonly Leg[] _legs = new Leg[2];
        private Vector3 _previousPosition, _direction;
        private float _phase, _weight;
        private bool _applied;

        private sealed class Bone
        {
            public Transform Transform;
            public Quaternion RestRotation, AnimatedRotation;
            public Vector3 RestPosition, AnimatedPosition;
        }

        private sealed class Leg
        {
            public Transform Thigh, Shin, Foot;
            public Vector3 RestFoot;
            public Quaternion RestFootRotation;
            public float UpperLength, LowerLength;
        }

        public HollowWardenLocomotion(Transform owner, Animator animator)
        {
            _owner = owner;
            _previousPosition = owner.position;
            _direction = owner.forward;
            if (animator == null) return;
            foreach (var transform in animator.GetComponentsInChildren<Transform>(true))
            {
                string name = transform.name;
                if (name != "Hips" && name != "Chest" && name != "Head" &&
                    !name.StartsWith("Thigh.") && !name.StartsWith("Shin.") &&
                    !name.StartsWith("Foot.") && !name.StartsWith("UpperArm.") &&
                    !name.StartsWith("Forearm.") && !name.StartsWith("Hand.")) continue;
                _bones[name] = new Bone { Transform = transform, RestRotation = transform.localRotation,
                    RestPosition = transform.localPosition };
            }
            for (int i = 0; i < _legs.Length; i++)
            {
                string side = i == 0 ? ".R" : ".L";
                if (!_bones.TryGetValue("Thigh" + side, out var thigh) ||
                    !_bones.TryGetValue("Shin" + side, out var shin) ||
                    !_bones.TryGetValue("Foot" + side, out var foot)) continue;
                _legs[i] = new Leg { Thigh = thigh.Transform, Shin = shin.Transform, Foot = foot.Transform,
                    RestFoot = owner.InverseTransformPoint(foot.Transform.position),
                    RestFootRotation = Quaternion.Inverse(owner.rotation) * foot.Transform.rotation,
                    UpperLength = Vector3.Distance(thigh.Transform.position, shin.Transform.position),
                    LowerLength = Vector3.Distance(shin.Transform.position, foot.Transform.position) };
            }
        }

        // Undo only our offsets before Animator evaluation, including when animation is culled.
        public void Restore()
        {
            if (!_applied) return;
            foreach (var bone in _bones.Values)
            {
                bone.Transform.localPosition = bone.AnimatedPosition;
                bone.Transform.localRotation = bone.AnimatedRotation;
            }
            _applied = false;
        }

        public void Apply(bool ready, float deltaTime)
        {
            Vector3 travel = Vector3.ProjectOnPlane(_owner.position - _previousPosition, Vector3.up);
            _previousPosition = _owner.position;
            // Spawns, teleports and long hitches must not inject a burst of footsteps.
            if (!ready || deltaTime <= 0 || deltaTime > .25f || travel.magnitude > 1f)
            {
                _weight = 0;
                return;
            }
            float distance = travel.magnitude;
            _weight = Mathf.MoveTowards(_weight, distance / deltaTime > .05f ? 1 : 0, deltaTime / .18f);
            _phase = Mathf.Repeat(_phase + distance / CycleDistance, 1);
            if (distance > .0001f) _direction = travel / distance;
            if (_weight <= 0 || _legs[0] == null || _legs[1] == null) return;

            foreach (var bone in _bones.Values)
            {
                bone.AnimatedPosition = bone.Transform.localPosition;
                bone.AnimatedRotation = bone.Transform.localRotation;
                bone.Transform.localPosition = Vector3.Lerp(bone.AnimatedPosition, bone.RestPosition, _weight);
                bone.Transform.localRotation = Quaternion.Slerp(bone.AnimatedRotation, bone.RestRotation, _weight);
            }
            _applied = true;
            float cycle = _phase * Mathf.PI * 2;
            float sway = Mathf.Sin(cycle);
            if (_bones.TryGetValue("Hips", out var hips))
            {
                // Lower the pelvis for bent supporting knees; rise over each planted leg.
                hips.Transform.position += (_owner.right * (sway * .045f) +
                    Vector3.up * (-.12f + .02f * Mathf.Cos(cycle * 2))) * _weight;
            }
            Rotate("Hips", 0, sway * 4, -sway * 2);
            Rotate("Chest", 3 + Mathf.Sin(cycle * 2) * 1.5f, -sway * 5, sway * 2.5f);
            Rotate("Head", -2, sway * 2, -sway);
            for (int i = 0; i < _legs.Length; i++)
            {
                float phase = Mathf.Repeat(_phase + i * .5f, 1);
                PoseLeg(_legs[i], phase);
                string side = i == 0 ? ".R" : ".L";
                float swing = Mathf.Cos(phase * Mathf.PI * 2 - .25f);
                // The larger right fist trails with a smaller arc than the left arm.
                Rotate("UpperArm" + side, swing * (i == 0 ? 11 : 16), 0, 0);
                Rotate("Forearm" + side, -5 - Mathf.Max(0, swing) * 9, 0, 0);
                Rotate("Hand" + side, Mathf.Sin(phase * Mathf.PI * 2 - .6f) * 4, 0, 0);
            }
        }

        private void Rotate(string name, float pitch, float yaw, float roll)
        {
            if (!_bones.TryGetValue(name, out var bone)) return;
            Quaternion offset = Quaternion.AngleAxis(yaw * _weight, _owner.up) *
                Quaternion.AngleAxis(pitch * _weight, _owner.right) *
                Quaternion.AngleAxis(roll * _weight, _owner.forward);
            bone.Transform.rotation = offset * bone.Transform.rotation;
        }

        private void PoseLeg(Leg leg, float phase)
        {
            float forward, lift = 0, pitch = 0;
            if (phase < Stance)
            {
                // Linear backward travel cancels the owner's forward translation during support.
                forward = StepTravel * (.5f - phase / Stance);
            }
            else
            {
                float swing = (phase - Stance) / (1 - Stance);
                forward = StepTravel * (Mathf.SmoothStep(0, 1, swing) - .5f);
                lift = Mathf.Sin(swing * Mathf.PI) * .19f;
                pitch = Mathf.Sin(swing * Mathf.PI * 2) * 12;
            }
            Vector3 target = _owner.TransformPoint(leg.RestFoot) + _direction * forward + Vector3.up * lift;
            target = Vector3.Lerp(leg.Foot.position, target, _weight);
            Quaternion footRotation = Quaternion.Slerp(leg.Foot.rotation,
                Quaternion.AngleAxis(pitch, _owner.right) * _owner.rotation * leg.RestFootRotation, _weight);
            Vector3 delta = target - leg.Thigh.position;
            float length = Mathf.Clamp(delta.magnitude, Mathf.Abs(leg.UpperLength - leg.LowerLength) + .001f,
                leg.UpperLength + leg.LowerLength - .001f);
            Vector3 axis = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(_owner.forward, axis).normalized;
            float along = (leg.UpperLength * leg.UpperLength - leg.LowerLength * leg.LowerLength + length * length) / (2 * length);
            float height = Mathf.Sqrt(Mathf.Max(0, leg.UpperLength * leg.UpperLength - along * along));
            Vector3 knee = leg.Thigh.position + axis * along + bend * height;
            leg.Thigh.rotation = Quaternion.FromToRotation(leg.Shin.position - leg.Thigh.position,
                knee - leg.Thigh.position) * leg.Thigh.rotation;
            leg.Shin.rotation = Quaternion.FromToRotation(leg.Foot.position - leg.Shin.position,
                target - leg.Shin.position) * leg.Shin.rotation;
            leg.Foot.rotation = footRotation;
        }
    }
}
