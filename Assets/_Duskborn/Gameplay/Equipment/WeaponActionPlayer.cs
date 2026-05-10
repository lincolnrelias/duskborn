using System;
using Duskborn.Core;
using Duskborn.Gameplay.ActionBar;
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

        private WeaponItem         _activeWeapon;
        private WeaponActionData   _activeData;
        private AnimationClip      _activeClip;
        private ActionContext      _activeCtx;
        private int                _activeActionIndex;
        private WeaponActionEvent[] _sortedEvents;
        private int                _eventCursor;
        private float              _blendWeight;
        private bool               _isPlaying;

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
            if (upperBodyMask != null)
                _layerMixer.SetLayerMaskFromAvatarMask(1, upperBodyMask);

            var output = AnimationPlayableOutput.Create(_graph, "WeaponAnimation", animator);
            output.SetSourcePlayable(_layerMixer);

            _graph.Play();
        }

        // Called by WeaponItem.OnLeftClick / OnRightClick.
        public void PlayAction(int actionIndex, WeaponItem weapon, ActionContext ctx)
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

            _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _layerMixer.ConnectInput(1, _clipPlayable, 0, 0f);

            DuskLog.Log(LogChannel.ActionBar,
                $"Weapon action [{actionIndex}] '{clip.name}' on '{weapon.DisplayName}'.");
        }

        private void StopCurrentAction()
        {
            if (!_isPlaying) return;
            _isPlaying   = false;
            _blendWeight = 0f;
            _layerMixer.SetInputWeight(1, 0f);
            if (_layerMixer.GetInput(1).IsValid()) _layerMixer.DisconnectInput(1);
            if (_clipPlayable.IsValid()) _clipPlayable.Destroy();
            _clipPlayable = default;
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
            while (_eventCursor < _sortedEvents.Length &&
                   normalized >= _sortedEvents[_eventCursor].NormalizedTime)
            {
                _activeWeapon.Behaviour?.OnActionEvent(
                    _sortedEvents[_eventCursor].Type, _activeActionIndex, _activeCtx);
                _eventCursor++;
            }

            if (normalized >= 1f) StopCurrentAction();
        }
    }
}
