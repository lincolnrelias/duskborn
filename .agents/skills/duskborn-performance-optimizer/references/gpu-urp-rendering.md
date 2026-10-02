# GPU, URP Shader, and Rendering Optimization

Duskborn uses a stylized *low-poly 3D* visual style with a *pixel art* interface, rendered through **Universal Render Pipeline (URP 17)** and enhanced with **Linework Lite** outlines.

The following principles target stable 60+ FPS on integrated and dedicated GPUs.

---

## 1. Preserve the SRP Batcher

The SRP Batcher groups compatible material draw calls without CPU overhead for rebuilding draw buffers.

- **Shader Compatibility**: Ensure every custom shader declares all material properties inside a uniform constant buffer (`CBUFFER_START(UnityPerMaterial)` ... `CBUFFER_END`).
- **Do not modify materials directly**: Using `material.color` or `material.SetFloat()` clones the material and disqualifies the object from the SRP Batcher.
- Keep palettes and atlased textures for *low-poly* models, minimizing material variation in the scene.

---

## 2. Horde Shadow Settings

Rendering dynamic shadow maps for hundreds of enemies overloads the GPU:

- For `Swarmer` and `Runner` (basic, fast enemies):
  - Set `Cast Shadows = Off` on the `MeshRenderer` component.
  - Enable shadows only for large enemies (`Brute`, `Elite`, Biome Bosses).
- Keep URP shadow cascades configured with restricted range in `UniversalRenderPipelineAsset`.

---

## 3. Linework Lite and Outlines (Rendering Layer Mask)

Duskborn's outline system identifies targets through rendering layers (`RenderingLayerMask`):

- Outlines must not add additional geometry passes.
- When enabling or disabling an outline on an enemy under the player's aim:
  - Modify only `renderer.renderingLayerMask = baseMask | outlineMask;`.
  - Never instantiate individual outline materials per enemy.

---

## 4. Pixel Art UI and Canvas Optimization

Duskborn's interface combines dynamic health bars, damage popups (`DamageNumberPool`), and menus:

1. **Canvas Separation**:
   - **Static Canvas**: Menus, static minimap, inventory frames.
   - **Dynamic Canvas**: Enemy health bars, floating damage popups, timers.
   - *Reason*: When one text element or damage number changes position or value, Unity resubmits that Canvas's entire mesh to the GPU. Separating Canvases isolates rebuilds.
2. **Raycast Target**:
   - Disable `Raycast Target` on all static images, TextMeshPro texts, and icons that do not receive direct mouse clicks.
