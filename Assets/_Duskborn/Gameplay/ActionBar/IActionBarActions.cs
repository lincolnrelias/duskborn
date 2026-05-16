using Duskborn.Gameplay;

namespace Duskborn.Gameplay.ActionBar
{
    public interface ILeftClickAction
    {
        void OnLeftClick(CombatContext ctx);
    }

    public interface IRightClickAction
    {
        void OnRightClick(CombatContext ctx);
    }
}
