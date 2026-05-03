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
    private Particle _freezeParticle;

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
        var owner = ownerSpell?.CastInfo?.Owner ?? buff.SourceUnit as ObjAIBase;
        var caster = owner ?? unit;

        _freezeParticle = AddParticleTarget(
            caster,
            null,
            "Global_Freeze",
            unit,
            -1f,
            bone: "BUFFBONE_GLB_GROUND_LOC"
        );
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        if (_freezeParticle != null)
        {
            RemoveParticle(_freezeParticle);
            _freezeParticle = null;
        }
    }

    public void OnUpdate(float diff)
    {
    }
}
