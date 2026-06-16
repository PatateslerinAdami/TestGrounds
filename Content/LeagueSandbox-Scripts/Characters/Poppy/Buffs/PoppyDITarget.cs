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

internal class PoppyDITarget : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; private set; } = new();
    private ObjAIBase _owner;
    private AttackableUnit _target;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _owner = ownerSpell.CastInfo.Owner;
        _target = unit;
        AddParticleTarget(_owner, unit, "DiplomaticImmunity_tar.troy", unit, buff.Duration);
    }
}
