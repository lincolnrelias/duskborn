using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Enemies;
using UnityEngine;

namespace Duskborn.Effects
{
    /// <summary>Actor effect composition, shared by enemies and player status controllers.</summary>
    public sealed class EnemyRuneVisuals : MonoBehaviour
    {
        private EnemyBase _enemy;
        private IDebuffSource _source;
        private readonly RuneAura[] _auras = new RuneAura[9];
        private readonly int[] _strengths = new int[9];
        private Bounds _body;
        private RuneSurface _surface;
        private void Start()
        {
            _enemy = GetComponent<EnemyBase>();
            _source = _enemy != null ? (IDebuffSource)_enemy : GetComponent<IDebuffSource>();
            _body = RuneAura.LocalBounds(transform);
            _body.size = Vector3.Min(_body.size, new Vector3(2f, 3f, 2f));
            _surface = gameObject.AddComponent<RuneSurface>();
        }
        private void LateUpdate()
        {
            if (_source == null) return;
            _enemy?.TickRuneEffects();
            int active = 0;
            for (int i = 1; i <= 8; i++)
            {
                bool show = _source.TryGetDebuff((RuneKind)i, out var view);
                _strengths[i] = show ? (view.Locked ? 8 : view.Stacks) : 0;
                if (show) active++;
            }
            _surface.SetEffects(_strengths, .35f);
            for (int i = 1; i <= 8; i++)
            {
                int strength = _strengths[i];
                if (_auras[i] == null && strength > 0)
                {
                    var go = new GameObject((RuneKind)i + " debuff"); go.transform.SetParent(transform, false);
                    _auras[i] = go.AddComponent<RuneAura>();
                }
                if (_auras[i] == null) continue;
                // Separate vertical bands and a common particle budget preserve all eight identities.
                Bounds region = _body;
                if (active > 1)
                {
                    float band = ((i - 1) * .618034f) % 1f;
                    region.center += Vector3.up * (band - .5f) * _body.size.y * .65f;
                    region.size = new Vector3(_body.size.x, _body.size.y * .45f, _body.size.z);
                }
                _auras[i].Configure((RuneKind)i, strength, region, false, .45f);
                _auras[i].SetDensity(1f / Mathf.Sqrt(Mathf.Max(1, active)));
            }
        }
    }
}
