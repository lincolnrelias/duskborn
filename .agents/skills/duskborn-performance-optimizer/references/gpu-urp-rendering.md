# Otimização de GPU, Shaders URP e Renderização

O Duskborn adota um estilo visual estilizado *low-poly 3D* com interface em *pixel art*, renderizado através do **Universal Render Pipeline (URP 17)** e aprimorado com o pacote **Linework Lite** para contornos.

Abaixo estão os princípios para garantir 60+ FPS estáveis em GPUs integradas e dedicadas.

---

## 1. Preservação do SRP Batcher

O SRP Batcher agrupa draw calls de materiais compatíveis sem custo de CPU para reconstrução de buffers de desenho.

- **Compatibilidade do Shader**: Certifique-se de que todos os shaders personalizados declarem todas as propriedades de material dentro de uma constante de buffer uniforme (`CBUFFER_START(UnityPerMaterial)` ... `CBUFFER_END`).
- **Não altere materiais diretamente**: Usar `material.color` ou `material.SetFloat()` clona o material e desqualifica o objeto do SRP Batcher.
- Mantenha paletas e texturas atlasadas para os modelos *low-poly*, minimizando a variação de materiais na cena.

---

## 2. Ajustes de Sombras para Hordas

Renderizar mapas de sombras dinâmicas para centenas de inimigos sobrecarrega a GPU:

- Em `Swarmer` e `Runner` (inimigos básicos e rápidos):
  - Configure `Cast Shadows = Off` no componente `MeshRenderer`.
  - Ative sombras apenas para inimigos de grande porte (`Brute`, `Elite`, Chefes de Bioma).
- Mantenha as cascatas de sombra URP configuradas com alcance restrito no `UniversalRenderPipelineAsset`.

---

## 3. Linework Lite e Contornos (Rendering Layer Mask)

O sistema de contorno do Duskborn identifica alvos via camadas de renderização (`RenderingLayerMask`):

- O contorno não deve adicionar passes adicionais de geometria.
- Ao ativar ou desativar o contorno de um inimigo sob a mira do jogador:
  - Modifique apenas `renderer.renderingLayerMask = baseMask | outlineMask;`.
  - Jamais instancie materiais de contorno individuais por inimigo.

---

## 4. Otimização de UI e Canvas em Pixel Art

A interface do Duskborn combina barras de vida dinâmicas, popups de dano (`DamageNumberPool`) e menus:

1. **Separação de Canvas**:
   - **Canvas Estático**: Menus, minimapa estático, molduras de inventário.
   - **Canvas Dinâmico**: Barras de vida de inimigos, popups de dano flutuante, contadores de tempo.
   - *Motivo*: Quando um único elemento de texto ou número de dano muda de posição ou valor, a Unity re-submete a malha inteira daquele Canvas para a GPU. Separar os Canvas isola as reconstruções.
2. **Raycast Target**:
   - Desmarque a opção `Raycast Target` em todas as imagens estáticas, textos do TextMeshPro e ícones que não recebem clique direto do mouse.
