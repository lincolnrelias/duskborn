using System;
using System.Collections.Generic;
using Duskborn.Audio;
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
    public partial class WeaponActionPlayer : MonoBehaviour
    {
        [SerializeField] private Animator    animator;
        [SerializeField] private AvatarMask  upperBodyMask;

        [Tooltip("Reduce bow spine tilt without locking torso heading against the hips. Disable to compare the original blend.")]
        [SerializeField] private bool stabilizeBowPose = true;

        private const float BlendInTime  = 0.08f;
        private const float BlendOutTime = 0.12f;

        private AuthoredBowPlayback _authoredBow;
        private AuthoredBowPlayback _retainedBowMovement;
        private AnimationMixerPlayable _aimLocomotionMixer;
        private double ActionTime => _authoredBow != null ? _authoredBow.Clock.ActionTime : _clipPlayable.IsValid() ? _clipPlayable.GetTime() : 0;
        private PlayableGraph               _graph;
        private AnimatorControllerPlayable  _controllerPlayable;
        private AnimationLayerMixerPlayable _layerMixer;
        private AnimationClipPlayable       _clipPlayable;
        private AvatarMask                  _fullBodyMask;
        private AvatarMask                  _fallbackUpperBodyMask;
        private AvatarMask                  _equippedActionMask;
        private BowPoseAnchor               _bowPoseAnchor;
        private BowLocomotionBodyAnchor     _bowLocomotionBodyAnchor;
        private bool                        _anchorBowPose;
        private bool                        _originalRootMotion;
        private PlayerController            _playerController;

        private WeaponItem          _activeWeapon;
        private WeaponSkill         _activeSkill;
        private WeaponActionData    _activeData;
        private AnimationClip       _activeClip;
        private bool                _ownsActiveClip;
        private CombatContext       _activeCtx;
        private int                 _activeActionIndex;
        private WeaponActionEvent[] _sortedEvents;
        private int                 _eventCursor;
        private float               _blendWeight;
        private bool                _isPlaying;
        private bool                _skillFired;
        private float               _runtimeSpeedMultiplier = 1f;
        private WeaponHitNotifier   _hitNotifier;

        private bool  _rangedAimAction;
        private bool  _shotRequested;
        private float _bowReleaseTime;
        private float _bowDuration;
        private float _aimPitch;

        private Transform _rangedDrawingHand;
        public Transform RangedDrawingHand
        {
            get
            {
                if (_rangedDrawingHand == null && animator != null && animator.isHuman)
                    _rangedDrawingHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                return _rangedDrawingHand;
            }
        }
        public Quaternion RangedAimRotation => transform.rotation * Quaternion.Euler(_aimPitch, 0f, 0f);

        public void SetRangedAimPitch(float pitch) => _aimPitch = Mathf.Clamp(pitch, -70f, 70f);

        public bool IsRangedAimAction => _isPlaying && _rangedAimAction;
        public bool IsDrawingBow => _isPlaying && (_activeWeapon?.Behaviour is RangedWeaponBehaviour) &&
            (!_shotRequested || (ActionTime < _bowReleaseTime));
        public float RangedDrawProgress => IsDrawingBow && _bowReleaseTime > 0f && _clipPlayable.IsValid()
            ? Mathf.Clamp01((_authoredBow != null && _authoredBow.Clock.Phase != AuthoredBowClock.Stage.Load ? 1f : (float)(ActionTime / (_authoredBow != null ? _authoredBow.Clock.LoadDuration : _bowReleaseTime)))) : 0f;

        public void RequestAimedShot()
        {
            if (IsRangedAimAction)
            {
                _shotRequested = true;
                _authoredBow?.Clock.RequestRelease();
                if (_clipPlayable.IsValid() && _activeData != null)
                    _clipPlayable.SetSpeed(_activeData.BaseSpeed * _runtimeSpeedMultiplier);
            }
        }

        // Combo chain state (see WeaponActionData.ComboChain).
        private int   _comboStep;
        private int   _comboActionIndex = -1;
        private float _comboExpiry;

        public bool              IsPlaying     => _isPlaying;
        public float             CurrentComboMultiplier { get; private set; } = 1f;
        public WeaponItem        CurrentWeapon => _activeWeapon;
        public WeaponSkill       CurrentSkill  => _activeSkill;
        public WeaponHitNotifier HitNotifier   =>
            _hitNotifier != null ? _hitNotifier : (_hitNotifier = GetComponent<WeaponHitNotifier>());

        public event Action         OnActionComplete;
        public event Action<float>  OnSpeedChanged;

        public float NormalizedTime => _isPlaying && _activeClip != null && _activeClip.length > 0f && _clipPlayable.IsValid()
            ? (float)(ActionTime / _activeClip.length) : 0f;

        public float RuntimeSpeedMultiplier
        {
            get => _runtimeSpeedMultiplier;
            set
            {
                _runtimeSpeedMultiplier = value;
                if (_isPlaying && !_rangedAimAction && _clipPlayable.IsValid())
                    _clipPlayable.SetSpeed(_activeData.BaseSpeed * value);
                OnSpeedChanged?.Invoke(_activeData != null ? _activeData.BaseSpeed * value : value);
            }
        }

        private WeaponAudioPlayer _audioPlayer;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            Duskborn.Gameplay.Player.IronrootAppearance.EnsureAnimationEventReceiver(animator);
        }

        private void Start()
        {
            _audioPlayer = GetComponent<WeaponAudioPlayer>();
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
            _authoredBow?.Dispose();
            _authoredBow = null;
            _retainedBowMovement?.Dispose();
            _retainedBowMovement = null;
            _bowPoseAnchor?.Dispose();
            _bowLocomotionBodyAnchor?.Dispose();
            if (_fullBodyMask != null)
            {
                if (Application.isPlaying) Destroy(_fullBodyMask);
                else DestroyImmediate(_fullBodyMask);
            }
            if (_fallbackUpperBodyMask != null)
            {
                if (Application.isPlaying) Destroy(_fallbackUpperBodyMask);
                else DestroyImmediate(_fallbackUpperBodyMask);
            }
        }

        private void BuildGraph()
        {
            _graph = PlayableGraph.Create($"{name}_WeaponActions");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _controllerPlayable = AnimatorControllerPlayable.Create(_graph, animator.runtimeAnimatorController);

            // Layer 0: full-body locomotion. Layer 1: upper-body weapon actions.
            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, 2);
            _aimLocomotionMixer = AnimationMixerPlayable.Create(_graph, 2);
            _aimLocomotionMixer.ConnectInput(0, _controllerPlayable, 0, 1f);
            if (BowPoseAnchor.Supports(animator))
                _bowLocomotionBodyAnchor = new BowLocomotionBodyAnchor(_graph,_aimLocomotionMixer,_layerMixer,animator);
            _layerMixer.ConnectInput(0, _bowLocomotionBodyAnchor != null ? _bowLocomotionBodyAnchor.Locomotion : (Playable)_aimLocomotionMixer, 0, 1f);
            _layerMixer.SetLayerAdditive(1, false);

            _fullBodyMask = new AvatarMask();
            _fallbackUpperBodyMask = new AvatarMask { name = "RuntimeUpperBodyMask" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                _fullBodyMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
                var part = (AvatarMaskBodyPart)i;
                bool upper = part != AvatarMaskBodyPart.Root && part != AvatarMaskBodyPart.LeftLeg &&
                    part != AvatarMaskBodyPart.RightLeg && part != AvatarMaskBodyPart.LeftFootIK &&
                    part != AvatarMaskBodyPart.RightFootIK;
                _fallbackUpperBodyMask.SetHumanoidBodyPartActive(part, upper);
            }
            _layerMixer.SetLayerMaskFromAvatarMask(1, ResolveMask(true, _equippedActionMask));

            _originalRootMotion = animator.applyRootMotion;
            _playerController   = GetComponent<PlayerController>();

            var output = AnimationPlayableOutput.Create(_graph, "WeaponAnimation", animator);
            if (BowPoseAnchor.Supports(animator))
                _bowPoseAnchor = new BowPoseAnchor(_graph, animator, _bowLocomotionBodyAnchor.Output);
            output.SetSourcePlayable(_bowPoseAnchor != null ? _bowPoseAnchor.Output : (Playable)_layerMixer);

            _graph.Play();
        }

        // Called by WeaponItem.OnLeftClick / OnRightClick.
        public void PlayAction(int actionIndex, WeaponItem weapon, CombatContext ctx, bool holdRangedDraw = false)
        {
            if (!_graph.IsValid())
            {
                DuskLog.Warn(LogChannel.ActionBar, "WeaponActionPlayer: graph not ready.");
                return;
            }

            var data = weapon?.Actions != null && actionIndex < weapon.Actions.Length
                ? weapon.Actions[actionIndex]
                : null;
            data?.EnsureMigrated();

            var entry = PickEntry(data, actionIndex);
            var clip  = entry?.Clip;
            if (clip == null)
            {
                DuskLog.Warn(LogChannel.ActionBar,
                    $"WeaponActionPlayer: no clip for '{weapon?.DisplayName}' action {actionIndex}.");
                return;
            }

            StopCurrentAction();
            // Reuse the complete movement branch, not just its clip times.
            // Recreating humanoid playables also resets their evaluation history.
            bool reuseMovement = holdRangedDraw && _retainedBowMovement != null &&
                _activeWeapon == weapon && _retainedBowMovement.Set == data.BowAnimations;
            if (!reuseMovement) ClearRetainedBowMovement();

            _activeWeapon      = weapon;
            _activeData        = data;
            _activeClip        = clip;
            _activeCtx         = ctx;
            _activeActionIndex = actionIndex;
            _eventCursor       = 0;
            _blendWeight       = 0f;
            _isPlaying         = true;

            bool ranged = weapon.Behaviour is RangedWeaponBehaviour;
            _rangedAimAction = holdRangedDraw && ranged &&
                RangedWeaponBehaviour.TryGetTiming(weapon.Actions, out _bowReleaseTime, out _bowDuration);
            _shotRequested = !_rangedAimAction;

            if (ranged)
            {
                // Vendor clips contain OnShoot/OnFinishAttack events for their demo
                // controller. Our typed timeline owns release, so strip the clone only.
                _activeClip = AuthoredBowPlayback.CloneClip(clip);
                _ownsActiveClip = true;
            }

            // Sorted defensive copy so we never mutate the SO array.
            _sortedEvents = SortedEvents(entry.Events);

            DuskLog.Log(LogChannel.Audio, $"PlayAction [{actionIndex}]: firing swing audio. audioPlayer={(object)_audioPlayer ?? "null"} profile={weapon?.AudioProfile?.name ?? "null"}.");
            _audioPlayer?.PlaySwing(weapon?.AudioProfile);

            ApplyMask(data.PreserveLocomotion, data.ResolveMask(weapon.ActionMask));
            _clipPlayable = AnimationClipPlayable.Create(_graph, _activeClip);
            _clipPlayable.SetApplyFootIK(false);
            _clipPlayable.SetSpeed(data.BaseSpeed * _runtimeSpeedMultiplier);
            _anchorBowPose = ranged && data.PreserveLocomotion && _bowPoseAnchor != null;
            Playable actionPose = _clipPlayable;
            if (_rangedAimAction && data.BowAnimations != null && data.BowAnimations.IsValid)
            {
                if (reuseMovement)
                {
                    _authoredBow = _retainedBowMovement;
                    _retainedBowMovement = null;
                    _authoredBow.RestartDraw();
                }
                else _authoredBow = new AuthoredBowPlayback(_graph, data.BowAnimations);
                _authoredBow.Advance(0f, 1f,
                    new Vector2(_controllerPlayable.GetFloat("VelocityX"), _controllerPlayable.GetFloat("VelocityY")));
                actionPose = _authoredBow.Pose;
                if (!reuseMovement) _aimLocomotionMixer.ConnectInput(1, _authoredBow.Movement, 0, 0f);
                SetAimLocomotionWeight();
                _clipPlayable.SetSpeed(0);
            }
            _bowPoseAnchor?.SetFacing(_authoredBow != null);
            if (!reuseMovement)
                _layerMixer.ConnectInput(1, _anchorBowPose ? _bowPoseAnchor.ConnectClip(actionPose) : actionPose, 0, 0f);
            else
            {
                // The retained pose tap is still connected to this same playback.
                // RestartDraw blends inside it; never dip into generic locomotion.
                _blendWeight = 1f;
                _layerMixer.SetInputWeight(1, 1f);
                _bowPoseAnchor?.SetWeight(_anchorBowPose && stabilizeBowPose ? 1f : 0f);
            }

            DuskLog.Log(LogChannel.ActionBar,
                $"Weapon action [{actionIndex}] '{clip.name}' on '{weapon.DisplayName}'.");

            if (ranged)
                ctx?.Combat?.BeginRangedAttack(weapon, _rangedAimAction);
        }

        // Combo chain: entries play in order while attacks land inside the reset window.
        // Non-combo: random variant. Either way the entry's damage multiplier applies.
        private WeaponActionClip PickEntry(WeaponActionData data, int actionIndex)
        {
            if (data == null || !data.HasEntries) return null;

            WeaponActionClip entry;
            if (data.ComboChain && data.Entries.Length > 1)
            {
                bool chained = actionIndex == _comboActionIndex && Time.time <= _comboExpiry;
                int  step    = chained ? _comboStep : 0;
                _comboActionIndex = actionIndex;
                _comboStep        = (step + 1) % data.Entries.Length;
                entry             = data.Entries[step];
                DuskLog.Log(LogChannel.Combat, $"Combo step {step + 1}/{data.Entries.Length}.");
            }
            else
                entry = data.PickRandom();

            CurrentComboMultiplier = entry != null && entry.DamageMultiplier > 0f
                ? entry.DamageMultiplier : 1f;
            return entry;
        }

        private static WeaponActionEvent[] SortedEvents(WeaponActionEvent[] events)
        {
            var sorted = events != null
                ? (WeaponActionEvent[])events.Clone()
                : Array.Empty<WeaponActionEvent>();
            Array.Sort(sorted, (a, b) => a.NormalizedTime.CompareTo(b.NormalizedTime));
            return sorted;
        }

        public void SetEquippedWeapon(WeaponItem weapon)
        {
            _equippedActionMask = weapon?.ActionMask;
            if (_graph.IsValid() && !_isPlaying)
                _layerMixer.SetLayerMaskFromAvatarMask(1, ResolveMask(true, _equippedActionMask));
        }

        private AvatarMask ResolveMask(bool preserveLocomotion, AvatarMask weaponMask)
        {
            if (!preserveLocomotion) return _fullBodyMask;
            return weaponMask != null ? weaponMask : upperBodyMask != null ? upperBodyMask : _fallbackUpperBodyMask;
        }

        private void ApplyMask(bool preserveLocomotion, AvatarMask weaponMask)
        {
            _layerMixer.SetLayerMaskFromAvatarMask(1, ResolveMask(preserveLocomotion, weaponMask));
            // Humanoid Body curves can affect the hip solve even with legs masked
            // out. Preserve locomotion's body pose for melee, skills and bow alike.
            _bowLocomotionBodyAnchor?.SetEnabled(preserveLocomotion);
            animator.applyRootMotion = !preserveLocomotion;
            if (!preserveLocomotion)
                _playerController?.SetInputEnabled(false);
        }

        private void StopCurrentAction()
        {
            if (!_isPlaying) return;
            FinishCurrentAction(_authoredBow != null && _activeData.PreserveLocomotion &&
                !_cancelling && _activeCtx?.Combat?.IsAiming == true);
        }

        private void FinishCurrentAction(bool retainMovement)
        {
            _isPlaying        = false;
            _rangedAimAction  = false;
            _shotRequested    = false;
            _blendWeight      = retainMovement ? 1f : 0f;
            _activeSkill      = null;
            _skillFired       = false;

            // Window for the next click to continue the chain starts when this action ends.
            if (_activeData != null && _activeData.ComboChain)
                _comboExpiry = Time.time + _activeData.ComboResetTime;
            animator.applyRootMotion = retainMovement ? false : _originalRootMotion;
            _playerController?.SetInputEnabled(true);
            if (retainMovement)
            {
                _retainedBowMovement = _authoredBow;
                _layerMixer.SetInputWeight(1, 1f);
                SetAimLocomotionWeight();
            }
            else
            {
                DisconnectActionPose();
                ResetAimLocomotion();
                _authoredBow?.Dispose();
            }
            _authoredBow = null;
            if (_clipPlayable.IsValid()) _clipPlayable.Destroy();
            _clipPlayable = default;
            if (_ownsActiveClip)
            {
                if (Application.isPlaying) Destroy(_activeClip);
                else DestroyImmediate(_activeClip);
            }
            _ownsActiveClip = false;
            if (!retainMovement)
                _layerMixer.SetLayerMaskFromAvatarMask(1, ResolveMask(true, _equippedActionMask));
            if (!_cancelling) OnActionComplete?.Invoke();
        }

        private void DisconnectActionPose()
        {
            _layerMixer.SetInputWeight(1, 0f);
            if (_layerMixer.GetInput(1).IsValid()) _layerMixer.DisconnectInput(1);
            _bowPoseAnchor?.DisconnectClip();
            _anchorBowPose = false;
            _bowPoseAnchor?.SetFacing(false);
        }

        private void ResetAimLocomotion()
        {
            _bowLocomotionBodyAnchor?.SetEnabled(false);
            if (!_aimLocomotionMixer.IsValid()) return;
            _aimLocomotionMixer.SetInputWeight(0, 1f);
            _aimLocomotionMixer.SetInputWeight(1, 0f);
            if (_aimLocomotionMixer.GetInput(1).IsValid()) _aimLocomotionMixer.DisconnectInput(1);
        }

        private void SetAimLocomotionWeight()
        {
            _bowLocomotionBodyAnchor?.SetEnabled(_activeData == null || _activeData.PreserveLocomotion);
            // Load/release only blend the upper body. The stride must continue
            // at full weight while grounded, including between repeated shots.
            float weight = _controllerPlayable.GetBool("IsGrounded") ? 1f : 0f;
            _aimLocomotionMixer.SetInputWeight(0, 1f - weight);
            _aimLocomotionMixer.SetInputWeight(1, weight);
        }

        private void ClearRetainedBowMovement()
        {
            if (_retainedBowMovement == null) return;
            DisconnectActionPose();
            ResetAimLocomotion();
            _retainedBowMovement.Dispose();
            _retainedBowMovement = null;
        }

        private bool _cancelling;
        public void CancelAction(bool blendOut = false, bool preserveAimMovement = false)
        {
            bool keepMovement = preserveAimMovement && _activeCtx?.Combat?.IsAiming == true;
            if (!_isPlaying)
            {
                if (!keepMovement) ClearRetainedBowMovement();
                return;
            }
            if (_activeWeapon?.Behaviour is RangedWeaponBehaviour) _activeCtx?.Combat?.CancelRangedAttack();
            // A server recovery retry rejects this shot, not the held aim gait.
            // Ordinary cancellation (dodge, death, weapon change, aim release)
            // still tears down both branches.
            _cancelling = !keepMovement;
            StopCurrentAction();
            _cancelling = false;
        }

        private void OnDisable() => CancelAction();

        public void PlaySkillAction(WeaponSkill skill, CombatContext ctx)
        {
            if (!_graph.IsValid())
            {
                DuskLog.Warn(LogChannel.ActionBar, "WeaponActionPlayer: graph not ready.");
                skill.Use(ctx);
                return;
            }

            var data = skill?.animation;
            data?.EnsureMigrated();
            var entry = data?.PickRandom();
            var clip  = entry?.Clip;
            if (clip == null)
            {
                skill?.Use(ctx);
                return;
            }

            StopCurrentAction();
            ClearRetainedBowMovement();

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

            _sortedEvents = SortedEvents(entry.Events);

            ApplyMask(data.PreserveLocomotion, data.ResolveMask(_equippedActionMask));
            _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _clipPlayable.SetApplyFootIK(false);
            _clipPlayable.SetSpeed(data.BaseSpeed * _runtimeSpeedMultiplier);
            _layerMixer.ConnectInput(1, _clipPlayable, 0, 0f);

            DuskLog.Log(LogChannel.ActionBar, $"Skill anim '{clip.name}' for '{skill.name}'.");
        }

        private void LateUpdate()
        {
            _bowPoseAnchor?.ApplyPose();
#if UNITY_EDITOR
            CaptureAnimationDiagnostics();
#endif
        }

        private void Update()
        {
            if (!_isPlaying)
            {
                if (_retainedBowMovement != null)
                {
                    if (_activeCtx?.Combat?.IsAiming != true) ClearRetainedBowMovement();
                    else
                    {
                        _retainedBowMovement.Advance(Time.deltaTime, 1f,
                            new Vector2(_controllerPlayable.GetFloat("VelocityX"), _controllerPlayable.GetFloat("VelocityY")),
                            _playerController != null && _playerController.IsOwner ? _playerController.PlanarSpeed : -1f);
                        SetAimLocomotionWeight();
                    }
                }
                return;
            }
            if ((_activeCtx?.Stats != null && !_activeCtx.Stats.IsAlive) ||
                (_activeCtx?.Caster is Duskborn.Gameplay.Enemies.EnemyBase enemy && !enemy.IsAlive))
            { CancelAction(); return; }

            float clipLength = _activeClip != null ? _activeClip.length : 1f;

            if (_authoredBow != null)
                _authoredBow.Advance(Time.deltaTime, _activeData.BaseSpeed * _runtimeSpeedMultiplier,
                    new Vector2(_controllerPlayable.GetFloat("VelocityX"), _controllerPlayable.GetFloat("VelocityY")),
                    _playerController != null && _playerController.IsOwner ? _playerController.PlanarSpeed : -1f);

            if (_authoredBow == null && _rangedAimAction && !_shotRequested && _clipPlayable.IsValid())
            {
                float holdTime = Mathf.Max(0f, _bowReleaseTime - 0.035f);
                if (_clipPlayable.GetTime() >= holdTime)
                {
                    _clipPlayable.SetSpeed(0f);
                    _clipPlayable.SetTime(holdTime);
                }
            }

            float normalized = clipLength > 0f && _clipPlayable.IsValid()
                ? (float)(ActionTime / clipLength) : 1f;

            // Smooth blend in at start, blend out at end.
            float blendInEnd    = Mathf.Min(BlendInTime  / clipLength, 0.5f);
            float blendOutStart = Mathf.Max(1f - BlendOutTime / clipLength, 0.5f);

            _blendWeight = normalized < blendInEnd
                ? Mathf.InverseLerp(0f, blendInEnd, normalized)
                : normalized > blendOutStart
                    ? Mathf.InverseLerp(1f, blendOutStart, normalized)
                    : 1f;

            if (_authoredBow != null)
            {
                // Fade only the initial entry into aim. Release and redraw keep
                // continuous upper-body ownership while their poses crossfade.
                _blendWeight = _authoredBow.HasPreviousPose ? 1f :
                    Mathf.Clamp01((float)_authoredBow.Clock.ActionTime / BlendInTime);
                SetAimLocomotionWeight();
                // Jump locomotion still needs the same bounded upper-body bow
                // correction. Ground contact only selects the lower-body gait.
                _bowPoseAnchor?.SetFacing(true);
            }
            _layerMixer.SetInputWeight(1, _blendWeight);
            _bowPoseAnchor?.SetWeight(_anchorBowPose && stabilizeBowPose ? _blendWeight : 0f);

            // Fire events whose threshold has been crossed this frame.
            if (_activeSkill != null)
            {
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

            if (normalized >= 1f || _authoredBow?.Clock.Phase == AuthoredBowClock.Stage.Complete) StopCurrentAction();
        }
    }
}
