namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Night 1 enemy. Fast, low HP, high count.
    /// Stats and skills are configured on the prefab via EnemyBase fields.
    /// Assign a CleaveSkill asset to the Skills array for the cleave ability.
    /// </summary>
    public class Swarmer : EnemyBase
    {
        protected override void Awake()
        {
            base.Awake();
            if (outlineRenderers != null)
            {
                for (int i = 0; i < outlineRenderers.Length; i++)
                {
                    if (outlineRenderers[i] != null)
                        outlineRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }
    }
}
