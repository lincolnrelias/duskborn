using Duskborn.Gameplay.Enchanting;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Effects
{
    public sealed class RunestonePickupVisual : MonoBehaviour
    {
        private SpriteRenderer _icon;
        private Sprite _sprite;
        private Camera _camera;
        private WeaponEtching _rune;
        public void Configure(WeaponEtching rune)
        {
            if (!rune.IsValid || _rune.Packed == rune.Packed) return;
            _rune = rune;
            var stone = Resources.Load<MaterialDefinition>("Runestones/" + RuneCatalog.Id(rune.kind, rune.level));
            if (stone?.Icon == null) return;
            if (_icon == null)
            {
                var go = new GameObject("Runestone sprite"); go.transform.SetParent(transform, false);
                _icon = go.AddComponent<SpriteRenderer>(); _icon.transform.localPosition = Vector3.up * .35f;
                _icon.transform.localScale = Vector3.one * .4f;
            }
            if (_sprite != null) Destroy(_sprite);
            _sprite = Sprite.Create(stone.Icon, new Rect(0, 0, stone.Icon.width, stone.Icon.height), Vector2.one * .5f, 256);
            _icon.sprite = _sprite;
            var aura = GetComponent<RuneAura>(); if (aura == null) aura = gameObject.AddComponent<RuneAura>();
            aura.Configure(rune.kind, rune.level, new Bounds(Vector3.up * .15f, Vector3.one * .25f));
        }
        private void LateUpdate()
        {
            if (_icon == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _icon.transform.rotation = _camera.transform.rotation;
        }
        private void OnDestroy() { if (_sprite != null) Destroy(_sprite); }
    }
}
