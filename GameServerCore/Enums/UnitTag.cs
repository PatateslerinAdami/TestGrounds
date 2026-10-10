using System;

namespace GameServerCore.Enums
{
    [Flags]
    public enum UnitTag
    {
        None = 0,
        Champion = 1 << 0,
        Champion_Clone = 1 << 1,
        Minion = 1 << 2,
        Minion_Lane = 1 << 3,
        Minion_Lane_Siege = 1 << 4,
        Minion_Lane_Super = 1 << 5,
        Minion_Summon = 1 << 6,
        Monster = 1 << 7,
        Monster_Epic = 1 << 8,
        Monster_Large = 1 << 9,
        Special = 1 << 10,
        Special_SyndraSphere = 1 << 11,
        Special_TeleportTarget = 1 << 12,
        Structure = 1 << 13,
        Structure_Inhibitor = 1 << 14,
        Structure_Nexus = 1 << 15,
        Structure_Turret = 1 << 16,
        Structure_Turret_Inhib = 1 << 17,
        Structure_Turret_Inner = 1 << 18,
        Structure_Turret_Nexus = 1 << 19,
        Structure_Turret_Outer = 1 << 20,
        Structure_Turret_Shrine = 1 << 21,
        Ward = 1 << 22
    }
}
