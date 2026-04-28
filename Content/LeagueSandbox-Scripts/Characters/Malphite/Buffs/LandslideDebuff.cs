using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class LandslideDebuff : IBuffGameScript {
    private ObjAIBase        _malphite;
    private Particle _particleL;
    private Particle _particleR;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _malphite = ownerSpell.CastInfo.Owner;
        var spellLevel = ownerSpell.CastInfo.SpellLevel;
        if (spellLevel is < 1 or > 5) return;
        StatsModifier.AttackSpeed.PercentBonus -= spellLevel switch {
            1 => 0.30f,
            2 => 0.35f,
            3 => 0.40f,
            4 => 0.45f,
            5 => 0.50f
        };

        unit.AddStatModifier(StatsModifier);
        _particleL = AddParticleTarget(_malphite, unit, "Landslide_buf", unit, buff.Duration, default, "L_hand");
        _particleR = AddParticleTarget(_malphite, unit, "Landslide_buf", unit, buff.Duration, default, "R_hand");
        
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_particleL);
        RemoveParticle(_particleR);
    }
}