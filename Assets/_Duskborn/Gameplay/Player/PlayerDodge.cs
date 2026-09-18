using FishNet.Object;
using UnityEngine;
using Duskborn.Effects;
using Duskborn.Gameplay.Hotkeys;

namespace Duskborn.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerDodge : NetworkBehaviour
    {
        [Header("Dodge")]
        [SerializeField] private float dodgeDistance  = 4f;
        [SerializeField] private float dodgeDuration  = 0.25f;
        [SerializeField] private float cooldown       = 1.5f;
        [SerializeField] private bool  invulnerable   = true;
        [SerializeField] private float iFrameDuration = 0.5f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [Tooltip("How long the body stays facing the roll direction. Match the Roll clip length.")]
        [SerializeField] private float rollAnimationDuration = 0.7f;

        private static readonly int HashRoll = Animator.StringToHash("Roll");

        private CharacterController _cc;
        private PlayerController    _controller;
        private PlayerStats         _stats;
        private AfterimageTrail     _trail;

        private Vector3 _rollDir;
        private float   _rollTimer;
        private float   _facingTimer;
        private float   _cooldownTimer;
        private System.Action _onDodge;

        public bool IsRolling => _rollTimer > 0f;

        private void Awake()
        {
            _cc         = GetComponent<CharacterController>();
            _controller = GetComponent<PlayerController>();
            _stats      = GetComponent<PlayerStats>();
            _onDodge    = TryDodge;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _trail = GetComponent<AfterimageTrail>();
            if (_trail == null) _trail = gameObject.AddComponent<AfterimageTrail>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;
            HotkeyManager.Instance?.Register(HotkeyManager.Dodge, _onDodge);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner) return;
            HotkeyManager.Instance?.Unregister(HotkeyManager.Dodge, _onDodge);
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.LeftControl))
                TryDodge();

            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (_facingTimer > 0f)
            {
                _facingTimer -= Time.deltaTime;
                if (_facingTimer <= 0f) _controller.SetRotationEnabled(true);
            }

            if (!IsRolling) return;

            _rollTimer -= Time.deltaTime;
            float speed = dodgeDistance / Mathf.Max(dodgeDuration, 0.01f);
            _cc.Move(_rollDir * (speed * Time.deltaTime) + Vector3.down * (2f * Time.deltaTime));

            if (!IsRolling) _controller.SetInputEnabled(true);
        }

        private void TryDodge()
        {
            if (!IsOwner || !_stats.IsAlive || IsRolling || _cooldownTimer > 0f || !_cc.isGrounded) return;

            _rollDir       = _controller.GetMoveDirectionWorld();
            _rollTimer     = dodgeDuration;
            _facingTimer   = Mathf.Max(rollAnimationDuration, dodgeDuration);
            _cooldownTimer = cooldown;
            _controller.SetInputEnabled(false);
            _controller.SetRotationEnabled(false);
            transform.rotation = Quaternion.LookRotation(_rollDir, Vector3.up);
            animator?.SetTrigger(HashRoll);
            _trail.EmitFor(dodgeDuration);

            if (invulnerable) RequestInvulnerabilityRpc(iFrameDuration);
        }

        [ServerRpc]
        private void RequestInvulnerabilityRpc(float duration)
        {
            // Server clamps to the configured duration so a client can't extend it.
            _stats.SetInvulnerable(Mathf.Min(duration, iFrameDuration));
        }
    }
}
