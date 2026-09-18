using UnityEngine;

namespace Duskborn.Effects
{
    [CreateAssetMenu(fileName = "HealthBarConfig", menuName = "Duskborn/Effects/Health Bar Config")]
    public class HealthBarConfig : ScriptableObject
    {
        [Header("Cores (Medieval Fantasy / Crepúsculo)")]
        [Tooltip("Vida plena: Esmeralda / Jade vibrante.")]
        public Color fullColor  = new Color(0.18f, 0.80f, 0.44f, 1f);
        [Tooltip("Vida moderada: Âmbar crepuscular / Ouro.")]
        public Color midColor   = new Color(0.92f, 0.62f, 0.14f, 1f);
        [Tooltip("Vida crítica: Rubi de sangue / Carmesim.")]
        public Color lowColor   = new Color(0.85f, 0.20f, 0.18f, 1f);
        [Tooltip("Fundo da calha: Tonalidade neutra para preservar o sprite da calha.")]
        public Color bgColor    = Color.white;
        [Tooltip("Rastro de dano fantasma: Brasa ardente do crepúsculo.")]
        public Color ghostColor = new Color(1.0f,  0.58f, 0.16f, 0.85f);
        [Tooltip("Tonalidade da moldura: Branca neutra para exibir arte estilizada de madeira e aço.")]
        public Color frameColor = Color.white;
        [Tooltip("Base da moldura externa: Ferro forjado.")]
        public Color ironColor  = new Color(0.14f, 0.16f, 0.20f, 1f);

        [Header("Limiares de Transição")]
        public float midThreshold = 0.5f;
        public float lowThreshold = 0.25f;

        [Header("Dinâmica & Temporização")]
        [Tooltip("Velocidade de decaimento suave da barra principal.")]
        public float drainSpeed      = 12f;
        [Tooltip("Velocidade do rastro fantasma de brasa (efeito de impacto).")]
        public float ghostDrainSpeed = 2.0f;
        [Tooltip("Tempo em segundos antes de iniciar o desvanecimento após dano.")]
        public float fadeDelay       = 3.5f;
        [Tooltip("Duração da animação de desvanecimento.")]
        public float fadeDuration    = 0.6f;

        [Header("Posicionamento & Enquadramento")]
        [Tooltip("Espaçamento vertical em unidades de mundo acima do topo do colisor/malha.")]
        public float yOffset = 0.45f;
        [Tooltip("Deslocamento horizontal em direção à câmera/jogador para exibir a barra à frente do objeto (objeto -> barra -> jogador).")]
        public float forwardOffset = 0.35f;
        [Tooltip("Altura máxima permitida acima da base da entidade. Impede que nós altos (como pinheiros ou monólitos) joguem a barra fora do campo visual.")]
        public float maxHeightAboveBase = 3.2f;
        [Tooltip("Prende a barra aos limites da tela visível caso o objeto seja alto ou a câmera aproxime.")]
        public bool clampToScreen = true;
        [Tooltip("Limite superior no viewport (0 a 1). 0.88 mantém a barra abaixo da barra superior de HUD.")]
        public float maxViewportY = 0.88f;
        [Tooltip("Limite inferior no viewport (0 a 1).")]
        public float minViewportY = 0.08f;
        [Tooltip("Margem lateral esquerda no viewport.")]
        public float minViewportX = 0.06f;
        [Tooltip("Margem lateral direita no viewport.")]
        public float maxViewportX = 0.94f;

        [Header("Identificação do Alvo (Tipografia)")]
        [Tooltip("Exibir nome ou tipo do alvo acima da barra de vida.")]
        public bool showName = true;
        [Tooltip("Cor do texto do nome do alvo (Ouro pergaminho).")]
        public Color nameTextColor = new Color(0.96f, 0.88f, 0.70f, 0.95f);
        [Tooltip("Tamanho da fonte em unidades de mundo.")]
        public float nameFontSize = 0.18f;
    }
}
