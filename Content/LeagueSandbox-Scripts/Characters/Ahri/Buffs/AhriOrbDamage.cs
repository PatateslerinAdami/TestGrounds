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

// S1 AhriOrbDamage.lua faithful port: the outbound-orb damage vehicle. Riot applies the Q's
// damage NOT from the missile/main spell directly but by adding this BUFF to the hit unit; the
// actual BBApplyDamage lives in OnBuffActivate. BuffAddType RENEW_EXISTING + MaxStack 1 = the
// dedup: a unit takes the damage exactly once per orb (renew only refreshes, does NOT re-fire
// OnActivate — verified AttackableUnit.cs:3454). Outbound = MAGIC.
//
// NOTE (scoped out, NOT ported yet): S1's OnBuffActivate ALSO runs Ahri's Essence Theft passive
// here — OrbofDeceptionIsActive charm-gate → Ahri_PassiveHeal + GlobalDrain + AhriSoulCrusher
// stack bookkeeping. That is a separate subsystem (needs the GlobalDrain / AhriSoulCrusher{,5,
// Counter} buffs + the OrbofDeceptionIsActive char var, none ported). Left out deliberately so
// this stays the damage-model test the change is about.
public class AhriOrbDamage : IBuffGameScript
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
        // Self-contained like S1's BBGetSlotSpellInfo(SPELLBOOK_CHAMPION, slot 0, OwnerVar=Attacker):
        // read the caster's champion Q straight off the buff's SourceUnit for level + coeff, rather
        // than depending on whatever originSpell the missile happened to pass in.
        var ahri = buff.SourceUnit;
        var q = ahri.Spells[0];
        var ap = ahri.Stats.AbilityPower.Total * q.SpellData.Coefficient;
        var dmg = q.SpellData.EffectLevelAmount[1][q.CastInfo.SpellLevel] + ap;

        unit.TakeDamage(ahri, dmg, DamageType.DAMAGE_TYPE_MAGICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}
