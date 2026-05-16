using System;
using System.Collections.Generic;
using Duskborn.Core;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Player;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Duskborn.Gameplay.Equipment
{
    /// <summary>
    /// Manages a PlayableGraph that layers weapon action clips over the locomotion
    /// AnimatorController via an upper-body avatar mask. Fires WeaponBehaviour events
    /// at the normalised times defined on each WeaponActionData.
    /// </summary>
    public class WeaponActionPlayer : MonoBehaviour
    {
        [SerializeField] private Animator    animator;
        [SerializeField] private AvatarMask  upperBodyMask;

        private const float BlendInTime  = 0.08f;
        private const float BlendOutTime = 0.12f;

        private PlayableGraph               _graph;
        private AnimatorControllerPlayable  _controllerPlayable;
        private AnimationLayerMixerPlayable _layerMixer;
        private AnimationClipPlayable       _clipPlayable;
        private AvatarMask                  _fullBodyMask;
        private bool                        _originalRootMotion;
        private PlayerController            _playerController;

        private WeaponItem         _activeWeapon;
        private WeaponSkill        _activeSkill;
        private WeaponActionData   _activeData;
        private AnimationClip      _activeClip;
        private CombatContext      _activeCtx;
        private int                _activeActionIndex;
        private WeaponActionEvent[] _sortedEvents;
        private int                _eventCursor;
        private float              _blendWeight;
        private bool               _isPlaying;
        private bool               _skillFired;
        private float              _runtimeSpeedMultiplier = 1f;

        public bool IsPlaying => _isPlaying;

        public event Action<float> OnSpeedChanged;

        public float RuntimeSpeedMultiplier
        {
            get => _runtimeSpeedMultiplier;
            set
            {
                _runtimeSpeedMultiplier = value;
                if (_isPlaying && _clipPlayable.IsValid())
                    _clipPlayable.SetSpeed(_activeData.BaseSpeed * value);
                OnSpeedChanged?.Invoke(_activeData != null ? _activeData.BaseSpeed * value : value);
            }
        }

        private void Start()
        {
            if (animator == null)
            {
                DuskLog.Warn(LogChannel.ActionBar, "WeaponActionPlayer: Animator reference not set.");
                return;
            }
            if (animator.runtimeAnimatorController != null)
                BuildGraph();
            else
                DuskLog.Warn(LogChannel.ActionBar, "WeaponActionPlayer: Animator has no controller — graph not built.");
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        private void BuildGraph()
        {
            _graph = PlayableGraph.Create($"{name}_WeaponActions");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _controllerPlayable = AnimatorControllerPlayable.Create(_graph, animator.runtimeAnimatorController);

            // Layer 0: full-body locomotion. Layer 1: upper-body weapon actions.
            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, 2);
            _layerMixer.ConnectInput(0, _controllerPlayable, 0, 1f);
            _layerMixer.SetLayerAdditive(1, false);

            _fullBodyMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                _fullBodyMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);

            _originalRootMotion = animator.applyRootMotion;
            _playerController   = GetComponent<PlayerController>();

            var output = AnimationPlayableOutput.Create(_graph, "WeaponAnimation", animator);
            output.SetSourcePlayable(_layerMixer);

            _graph.Play();
        }

        // Called by WeaponItem.OnLeftClick / OnRightClick.
        public void PlayAction(int actionIndex, WeaponItem weapon, CombatContext ctx)
        {
            if (!_graph.IsValid())
            {
                DuskLog.Warn(LogChannel.ActionBar, "WeaponActionPlayer: graph not ready.");
                return;
            }

            var data = weapon?.Actions != null && actionIndex < weapon.Actions.Length
                ? weapon.Actions[actionIndex]
                : null;

            var clip = data?.PickClip();
            if (clip == null)
            {
                DuskLog.Warn(LogChannel.ActionBar,
                    $"WeaponActionPlayer: no clip for '{weapon?.DisplayName}' action {actionIndex}.");
                return;
            }

            StopCurrentAction();

            _activeWeapon      = weapon;
            _activeData        = data;
            _activeClip        = clip;
            _activeCtx         = ctx;
            _activeActionIndex = actionIndex;
            _eventCursor       = 0;
            _blendWeight       = 0f;
            _isPlaying         = true;

            // Sorted defensive copy so we never mutate the SO array.
            _sortedEvents = data.Events != null
                ? (WeaponActionEvent[])data.Events.Clone()
                : Array.Empty<WeaponActionEvent>();
            Array.Sort(_sortedEvents, (a, b) => a.NormalizedTime.CompareTo(b.NormalizedTime));

            ApplyMask(data.PreserveLocomotion);
            _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _clipPlayable.SetSpeed(data.BaseSpeed * _runtimeSpeedMultiplier);
            _layerMixer.ConnectInput(1, _clipPlayable, 0, 0f);

            DuskLog.Log(LogChannel.ActionBar,
                $"Weapon action [{actionIndex}] '{clip.name}' on '{weapon.DisplayName}'.");
        }

        private void ApplyMask(bool preserveLocomotion)
        {
            var mask = (preserveLocomotion && upperBodyMask != null) ? upperBodyMask : _fullBodyMask;
            _layerMixer.SetLayerMaskFromAvatarMask(1, mask);
            animator.applyRootMotion = !preserveLocomotion;
            if (!preserveLocomotion)
                _playerController?.SetInputEnabled(false);
        }

        private void StopCurrentAction()
        {
            if (!_isPlaying) return;
            _isPlaying   = false;
            _blendWeight = 0f;
            _activeSkill = null;
            _skillFired  = false;
            animator.applyRootMotion = _originalRootMotion;
            _playerController?.SetInputEnabled(true);
            _layerMixer.SetInputWeight(1, 0f);
            if (_layerMixer.GetInput(1).IsValid()) _layerMixer.DisconnectInput(1);
            if (_clipPlayable.IsValid()) _clipPlayable.Destroy();
            _clipPlayable = default;
        }

        public void PlaySkillAction(WeaponSkill skill, CombatContext ctx)
        {
            if (!_graph.IsValid())
            {
                DuskLog.Warn(LogChannel.ActionBar, "WeaponActionPlayer: graph not ready.");
                skill.Use(ctx);
                return;
            }

            var data = skill?.animation;
            var clip = data?.PickClip();
            if (clip == null)
            {
                skill?.Use(ctx);
                return;
            }

            StopCurrentAction();

            _activeSkill       = skill;
            _activeWeapon      = null;
            _activeData        = data;
            _activeClip        = clip;
            _activeCtx         = ctx;
            _activeActionIndex = -1;
            _eventCursor       = 0;
            _blendWeight       = 0f;
            _isPlaying         = true;
            _skillFired        = false;

            _sortedEvents = data.Events != null
                ? (WeaponActionEvent[])data.Events.Clone()
                : Array.Empty<WeaponActionEvent>();
            Array.Sort(_sortedEvents, (a, b) => a.NormalizedTime.CompareTo(b.NormalizedTime));

            ApplyMask(data.PreserveLocomotion);
            _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _clipPlayable.SetSpeed(data.BaseSpeed * _runtimeSpeedMultiplier);
            _layerMixer.ConnectInput(1, _clipPlayable, 0, 0f);

            DuskLog.Log(LogChannel.ActionBar, $"Skill anim '{clip.name}' for '{skill.name}'.");
        }

        private void Update()
        {
            if (!_isPlaying) return;

            float clipLength = _activeClip.length;
            float normalized = clipLength > 0f ? (float)(_clipPlayable.GetTime() / clipLength) : 1f;

            // Smooth blend in at start, blend out at end.
            float blendInEnd    = Mathf.Min(BlendInTime  / clipLength, 0.5f);
            float blendOutStart = Mathf.Max(1f - BlendOutTime / clipLength, 0.5f);

            _blendWeight = normalized < blendInEnd
                ? Mathf.InverseLerp(0f, blendInEnd, normalized)
                : normalized > blendOutStart
                    ? Mathf.InverseLerp(1f, blendOutStart, normalized)
                    : 1f;

            _layerMixer.SetInputWeight(1, _blendWeight);

            // Fire events whose threshold has been crossed this frame.
            if (_activeSkill != null)
            {
                // Skill: Use() fires once at the first event threshold (or immediately if none).
                if (!_skillFired)
                {
                    bool ready = _sortedEvents.Length == 0 ||
                                 normalized >= _sortedEvents[0].NormalizedTime;
                    if (ready) { _activeSkill.Use(_activeCtx); _skillFired = true; }
                }
            }
            else
            {
                while (_eventCursor < _sortedEvents.Length &&
                       normalized >= _sortedEvents[_eventCursor].NormalizedTime)
                {
                    _activeWeapon?.Behaviour?.OnActionEvent(
                        _sortedEvents[_eventCursor].Type, _activeActionIndex, _activeCtx);
                    _eventCursor++;
                }
            }

            if (normalized >= 1f) StopCurrentAction();
        }
    }
}
