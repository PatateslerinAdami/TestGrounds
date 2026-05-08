using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class IcebornGauntletSlow : IBuffGameScript
{
    private Particle _freezeParticle;
    private AttackableUnit _unit;

    public BuffScriptMetaData BuffMetaData { get; } = new()
    {
        BuffType = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _unit = unit;

        var slowPercent = buff.Variables.GetFloat("slowPercent", 0.30f);
        if (slowPercent < 0.0f)
        {
            slowPercent = -slowPercent;
        }

        StatsModifier.MoveSpeed.PercentBonus -= slowPercent;
        unit.AddStatModifier(StatsModifier);

        var owner = ownerSpell?.CastInfo?.Owner ?? buff.SourceUnit as ObjAIBase;
        var caster = owner ?? unit;

        _freezeParticle = AddParticleTarget(
            caster,
            null,
            "Global_Freeze",
            unit,
            buff.Duration,
            bone: "BUFFBONE_GLB_GROUND_LOC"
        );
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        KillParticle();
    }

    public void OnUpdate(float diff)
    {
        if (_unit != null && _unit.IsDead)
        {
            KillParticle();
        }
    }

    private void KillParticle()
    {
        _freezeParticle?.SetToRemove();
    }
}
