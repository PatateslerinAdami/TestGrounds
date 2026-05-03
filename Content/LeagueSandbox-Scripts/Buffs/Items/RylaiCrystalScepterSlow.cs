using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class RylaiCrystalScepterSlow : IBuffGameScript
{
    private const string ImpactParticleName = "Global_Slow.troy";
    private const string PersistentParticleName = "Global_Slow_buf.troy";

    private Particle _slowParticle;

    public BuffScriptMetaData BuffMetaData { get; } = new()
    {
        BuffType = BuffType.SLOW,

        // Important:
        // REPLACE_EXISTING is intentional here.
        // Rylai can apply 15% or 35% slow depending on DamageSource.
        // RENEW_EXISTING would refresh duration without updating slowPercent.
        BuffAddType = BuffAddType.REPLACE_EXISTING,

        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        var slowPercent = buff.Variables.GetFloat("slowPercent", 0.15f);
        if (slowPercent < 0.0f)
        {
            slowPercent = -slowPercent;
        }

        StatsModifier.MoveSpeed.PercentBonus -= slowPercent;
        unit.AddStatModifier(StatsModifier);

        var caster = buff.SourceUnit ?? unit;

        // Short impact particle.
        AddParticleTarget(
            caster,
            unit,
            ImpactParticleName,
            unit,
            0.35f
        );

        // Persistent slow particle for the duration of the debuff.
        _slowParticle = AddParticleTarget(
            caster,
            unit,
            PersistentParticleName,
            unit,
            buff.Duration
        );
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_slowParticle);
        _slowParticle = null;
    }

    public void OnUpdate(float diff)
    {
    }
}
