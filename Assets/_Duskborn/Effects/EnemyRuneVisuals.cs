using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Enemies;
using UnityEngine;

namespace Duskborn.Effects
{
    public sealed class EnemyRuneVisuals : MonoBehaviour
    {
        private EnemyBase _enemy;
        private readonly RuneAura[] _auras = new RuneAura[9];
        private readonly int[] _previous = new int[9];
        private readonly int[] _displayStacks = new int[9];
        private Bounds _body;
        private RuneStatusBar _bar;
        private void Start()
        {
            _enemy = GetComponent<EnemyBase>();
            _body = RuneAura.LocalBounds(transform);
            _body.size = Vector3.Min(_body.size, new Vector3(2f, 3f, 2f));
        }
        private void LateUpdate()
        {
            if (_enemy == null) return;
            _enemy.TickRuneEffects();
            bool changed = false;
            for (int i = 1; i <= 8; i++)
            {
                int stacks = _enemy.IsAlive ? _enemy.RuneStacks((RuneKind)i) : 0;
                int display = stacks;
                if (i == (int)_enemy.RuneLockKind && _enemy.RuneFrozen && _enemy.IsAlive && display == 0) display = -1;
                if (_displayStacks[i] != display) { _displayStacks[i] = display; changed = true; }
                if (i == (int)_enemy.RuneLockKind && _enemy.RuneFrozen && _enemy.IsAlive) stacks = 8;
                if (_previous[i] == stacks) continue;
                changed = true;
                _previous[i] = stacks;
                if (_auras[i] == null && stacks > 0)
                {
                    var go = new GameObject((RuneKind)i + " debuff"); go.transform.SetParent(transform, false);
                    _auras[i] = go.AddComponent<RuneAura>();
                }
                _auras[i]?.Configure((RuneKind)i, stacks, _body);
            }
            if (changed)
            {
                if (_bar == null)
                {
                    var go = new GameObject("Rune status bar", typeof(RectTransform)); go.transform.SetParent(transform, false);
                    _bar = go.AddComponent<RuneStatusBar>(); _bar.Configure(_body.max.y + .25f);
                }
                _bar.SetStacks(_displayStacks);
            }
        }
    }
}
