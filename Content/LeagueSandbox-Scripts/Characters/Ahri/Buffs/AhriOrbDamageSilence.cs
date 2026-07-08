using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

// S1 AhriOrbDamageSilence.lua faithful port: the RETURN-orb damage vehicle. Identical structure
// to AhriOrbDamage but SourceDamageType TRUE_DAMAGE (S1: DamageType = TRUE_DAMAGE) — the orb's
// second pass is unmitigated. Separate buff name from AhriOrbDamage so a unit caught by BOTH the
// outbound and return orb takes both hits (each buff fires its own OnActivate once).
//
// NOTE (scoped out, same as AhriOrbDamage): the Essence Theft passive block in S1's OnBuffActivate
// is not ported here — separate subsystem.
public class AhriOrbDamageSilence : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType    = BuffType.INTERNAL,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        // Self-contained: read the caster's champion Q off the buff's SourceUnit (see AhriOrbDamage).
        var ahri = buff.SourceUnit;
        var q = ahri.Spells[0];
        var ap = ahri.Stats.AbilityPower.Total * q.SpellData.Coefficient;
        var dmg = q.SpellData.EffectLevelAmount[1][q.CastInfo.SpellLevel] + ap;

        unit.TakeDamage(ahri, dmg, DamageType.DAMAGE_TYPE_TRUE,
            DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}
