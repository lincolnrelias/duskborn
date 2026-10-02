using UnityEngine;

namespace Duskborn.Effects
{
    [CreateAssetMenu(fileName = "HealthBarConfig", menuName = "Duskborn/Effects/Health Bar Config")]
    public class HealthBarConfig : ScriptableObject
    {
        [Header("Colors (Medieval Fantasy / Dusk)")]
        [Tooltip("Full health: vibrant Emerald / Jade.")]
        public Color fullColor  = new Color(0.18f, 0.80f, 0.44f, 1f);
        [Tooltip("Moderate health: twilight Amber / Gold.")]
        public Color midColor   = new Color(0.92f, 0.62f, 0.14f, 1f);
        [Tooltip("Critical health: blood Ruby / Crimson.")]
        public Color lowColor   = new Color(0.85f, 0.20f, 0.18f, 1f);
        [Tooltip("Track background: neutral tint to preserve the track sprite.")]
        public Color bgColor    = Color.white;
        [Tooltip("Ghost damage trail: burning twilight ember.")]
        public Color ghostColor = new Color(1.0f,  0.58f, 0.16f, 0.85f);
        [Tooltip("Frame tint: neutral white to display stylized wood and steel artwork.")]
        public Color frameColor = Color.white;
        [Tooltip("Outer frame base: wrought iron.")]
        public Color ironColor  = new Color(0.14f, 0.16f, 0.20f, 1f);

        [Header("Transition Thresholds")]
        public float midThreshold = 0.5f;
        public float lowThreshold = 0.25f;

        [Header("Dynamics & Timing")]
        [Tooltip("Smooth decay speed of the main bar.")]
        public float drainSpeed      = 12f;
        [Tooltip("Ember ghost trail speed (impact effect).")]
        public float ghostDrainSpeed = 2.0f;
        [Tooltip("Time in seconds before fading begins after damage.")]
        public float fadeDelay       = 3.5f;
        [Tooltip("Fade animation duration.")]
        public float fadeDuration    = 0.6f;

        [Header("Positioning and Framing")]
        [Tooltip("Vertical spacing in world units above the collider / mesh top.")]
        public float yOffset = 0.45f;
        [Tooltip("Horizontal offset toward the camera / player to display the bar in front of the object (object -> bar -> player).")]
        public float forwardOffset = 0.35f;
        [Tooltip("Maximum height above the entity base. Prevents tall nodes (such as pines or monoliths) from pushing the bar out of view.")]
        public float maxHeightAboveBase = 3.2f;
        [Tooltip("Clamp the bar to visible screen bounds when the object is tall or the camera approaches.")]
        public bool clampToScreen = true;
        [Tooltip("Upper viewport limit (0 to 1). 0.88 keeps the bar below the top HUD bar.")]
        public float maxViewportY = 0.88f;
        [Tooltip("Lower viewport limit (0 to 1).")]
        public float minViewportY = 0.08f;
        [Tooltip("Left viewport margin.")]
        public float minViewportX = 0.06f;
        [Tooltip("Right viewport margin.")]
        public float maxViewportX = 0.94f;

        [Header("Target Identification (Typography)")]
        [Tooltip("Display the target name or type above the health bar.")]
        public bool showName = true;
        [Tooltip("Target name text color (parchment gold).")]
        public Color nameTextColor = new Color(0.96f, 0.88f, 0.70f, 0.95f);
        [Tooltip("Font size in world units.")]
        public float nameFontSize = 0.18f;
    }
}
