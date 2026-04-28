using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class ObduracyBuff : IBuffGameScript {
    private ObjAIBase        _malphite;
    private Particle _enrageParticle;
    private Particle _enrageParticleL;
    private Particle _enrageParticleR;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _malphite = ownerSpell.CastInfo.Owner;
        var spellLevel = ownerSpell.CastInfo.SpellLevel;
        
        if (spellLevel is < 1 or > 5) return;
        var inc = 0.20f + 0.05f * (spellLevel - 1f);
        StatsModifier.Armor.PercentBonus = inc;
        StatsModifier.AttackDamage.PercentBonus = inc;
        unit.AddStatModifier(StatsModifier);
        
        _enrageParticle = AddParticleTarget(_malphite, _malphite, "Malphite_Enrage_glow", _malphite, buff.Duration, default, "root");
        _enrageParticleL = AddParticleTarget(_malphite, unit, "Malphite_Enrage_buf", unit, buff.Duration, default, "L_finger_b");
        _enrageParticleR = AddParticleTarget(_malphite, unit, "Malphite_Enrage_buf", unit, buff.Duration, default, "R_thumb_b");
        _malphite.SetAutoAttackSpell("ObduracyAttack", true);

    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_enrageParticle);
        RemoveParticle(_enrageParticleL);
        RemoveParticle(_enrageParticleR);
        _malphite.ResetAutoAttackSpell();
    }
}