using System.Collections.Generic;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Effects
{
    public class DamageNumberPool : MonoBehaviour
    {
        public static DamageNumberPool Instance { get; private set; }

        [SerializeField] private FloatingDamageNumber prefab;
        [SerializeField] private int                  initialSize = 20;

        private readonly Stack<FloatingDamageNumber> _pool = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            for (int i = 0; i < initialSize; i++)
                _pool.Push(CreateInstance());

            DuskLog.Log(LogChannel.Effects, $"DamageNumberPool pre-warmed with {initialSize} instances.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Get(Vector3 worldPos, float amount, bool isCrit, DamageNumberConfig config)
        {
            if (config == null)
            {
                DuskLog.Warn(LogChannel.Effects, "DamageNumberPool.Get: config is null — skipping.");
                return;
            }

            Vector3 spread = new Vector3(
                Random.Range(-config.horizontalSpread, config.horizontalSpread), 0f, 0f);

            FloatingDamageNumber num = _pool.Count > 0 ? _pool.Pop() : CreateInstance();
            num.Play(worldPos + spread, amount, isCrit, config);

            DuskLog.Log(LogChannel.Effects,
                $"DamageNumberPool: amount={amount:F1} isCrit={isCrit} poolRemaining={_pool.Count}");
        }

        public void Return(FloatingDamageNumber num)
        {
            num.gameObject.SetActive(false);
            num.transform.SetParent(transform);
            _pool.Push(num);
        }

        private FloatingDamageNumber CreateInstance()
        {
            var go  = Instantiate(prefab.gameObject, transform);
            var num = go.GetComponent<FloatingDamageNumber>();
            go.SetActive(false);
            return num;
        }
    }
}
