using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Buffs
{
    // The "PetCommanded" marker buff (texture 27.dds) the PetAI puts on the unit a hard pet command
    // targets (attack target / owner / the pet itself). Cosmetic/data-only — the client renders the
    // buff icon from its own PetCommanded data; this script just exists so the buff loads with explicit
    // metadata (BuffType INTERNAL = Riot PET_COMMAND_BUFF_TYPE 0) instead of the empty-script fallback,
    // which also silences the "Could not find script: Buffs.PetCommanded" warning.
    internal class PetCommanded : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new()
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
        };

        public StatsModifier StatsModifier { get; } = new();
    }
}
