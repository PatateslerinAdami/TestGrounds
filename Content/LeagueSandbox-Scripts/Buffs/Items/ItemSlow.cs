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

internal class ItemSlow : IBuffGameScript
{
    private Particle _particle;
    private AttackableUnit _unit;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsHidden = false
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _unit = unit;

        var owner = ownerSpell?.CastInfo?.Owner ?? buff.SourceUnit as ObjAIBase;
        var caster = owner ?? unit;

        _particle = AddParticleTarget(
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
        if (_unit == null)
        {
            KillParticle();
            return;
        }

        if (_unit.IsDead)
        {
            KillParticle();
        }
    }

    private void KillParticle()
    {
        if (_particle == null)
        {
            return;
        }

        _particle.SetToRemove();
        _particle = null;
    }
}
