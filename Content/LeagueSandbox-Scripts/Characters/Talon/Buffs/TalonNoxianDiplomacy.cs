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

internal class TalonNoxianDiplomacyBuff : IBuffGameScript
{
    private ObjAIBase _talon;
    private Particle _p1;
    private Particle _p2;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _talon = ownerSpell.CastInfo.Owner;
        _p1 = AddParticleTarget(_talon, _talon, "talon_Q_on_hit_ready_01", _talon, buff.Duration, bone: "L_Hand");
        _p2 = AddParticleTarget(_talon, _talon, "talon_Q_on_hit_ready_01", _talon, buff.Duration, bone: "R_Hand");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
    }
}