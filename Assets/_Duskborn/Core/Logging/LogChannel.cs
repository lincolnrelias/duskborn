namespace Duskborn
{
    public enum LogChannel
    {
        DayNightCycle    = 0,
        Network          = 1,
        GameState        = 2,
        GameSession      = 3,
        Combat           = 4,
        Warrior          = 5,
        Ranger           = 6,
        Mage             = 7,
        PlayerClass      = 8,
        PlayerInteractor = 9,
        Inventory        = 10,
        Enemy            = 11,
        Wave             = 12,
        Loot             = 13,
        DebugController  = 14,
        HitboxDebugger   = 15,
        ActionBar        = 16,
        Audio            = 17,
    }

    [System.Flags]
    public enum LogChannelMask
    {
        None             = 0,
        DayNightCycle    = 1 << 0,
        Network          = 1 << 1,
        GameState        = 1 << 2,
        GameSession      = 1 << 3,
        Combat           = 1 << 4,
        Warrior          = 1 << 5,
        Ranger           = 1 << 6,
        Mage             = 1 << 7,
        PlayerClass      = 1 << 8,
        PlayerInteractor = 1 << 9,
        Inventory        = 1 << 10,
        Enemy            = 1 << 11,
        Wave             = 1 << 12,
        Loot             = 1 << 13,
        DebugController  = 1 << 14,
        HitboxDebugger   = 1 << 15,
        ActionBar        = 1 << 16,
        Audio            = 1 << 17,
        All              = ~0,
    }
}
