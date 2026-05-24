using System.Collections;
using Duskborn;
using Duskborn.Gameplay.Equipment;
using UnityEngine;

namespace Duskborn.Effects
{
    public class WeaponEffectPlayer : MonoBehaviour
    {
        private WeaponActionPlayer _actionPlayer;

        private void Awake() => _actionPlayer = GetComponent<WeaponActionPlayer>();

        public void SpawnHitEffect(string tag, Vector3 position)
        {
            var skill  = _actionPlayer?.CurrentSkill;
            var weapon = _actionPlayer?.CurrentWeapon;

            DuskLog.Log(LogChannel.Effects,
                $"[{name}] SpawnHitEffect tag='{tag}' pos={position} " +
                $"skill={skill?.name ?? "none"} weapon={weapon?.DisplayName ?? "none"}");

            var prefab = skill?.effectProfile?.PickEffect(tag)
                      ?? weapon?.EffectProfile?.PickEffect(tag);

            if (prefab == null)
            {
                DuskLog.Warn(LogChannel.Effects,
                    $"[{name}] no prefab resolved for tag='{tag}' — " +
                    $"skill.effectProfile={skill?.effectProfile?.name ?? "null"} " +
                    $"weapon.effectProfile={weapon?.EffectProfile?.name ?? "null"}");
                return;
            }

            DuskLog.Log(LogChannel.Effects, $"[{name}] spawning '{prefab.name}' at {position}.");
            var go = Instantiate(prefab, position, Quaternion.identity);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null) StartCoroutine(DestroyWhenDone(go, ps));
            else            Destroy(go, 5f);
        }

        private IEnumerator DestroyWhenDone(GameObject go, ParticleSystem ps)
        {
            yield return new WaitUntil(() => !ps.IsAlive(withChildren: true));
            Destroy(go);
        }
    }
}
