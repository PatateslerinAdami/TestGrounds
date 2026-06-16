using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class PoppyParagonOfDemacia : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
        NotSingleTargetSpell = true,
        CastingBreaksStealth = true,
    };

    public void OnSpellCast(Spell spell)
    {
        var owner = spell.CastInfo.Owner;

        // Speed buff — visible icon (PoppyParagonSpeed, COMBAT_ENCHANCER)
        AddBuff("PoppyParagonSpeed", 5f, 1, spell, owner, owner);
        // Attack speed stat (PoppyParagonAS, INTERNAL) — was missing, the icon buff had no stats
        AddBuff("PoppyParagonAS", 5f, 1, spell, owner, owner, false);

        // Paragon stacks — ICON + STATS (PoppyParagonStats: COMBAT_ENCHANCER, STACKS_AND_RENEWS)
        // Client runs PoppyParagonStats.luaobj → BuffTextureName = PoppyDefenseOfDemacia.dds
        for (int i = 0; i < 10; i++)
            AddBuff("PoppyParagonStats", 5f, 1, spell, owner, owner, false);
    }
}
