using System.Threading;
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

public class SwainTorment : IBuffGameScript
{
    private PeriodicTicker _periodicTicker;
    private Spell _spell;
    private AttackableUnit _unit;
    private ObjAIBase _owner;
    private float _damage;
    private Particle _p, _p2;
    private Buff _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.DAMAGE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _unit = unit;
        _owner = ownerSpell.CastInfo.Owner;
        _buff = buff;
        _spell = ownerSpell;
        _p = AddParticleTarget(_owner, unit, "swain_torment_dot", unit, buff.Duration);
        _p2 = AddParticleTarget(_owner, unit, "swain_torment_marker", unit);
        ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
    }

    public void OnUpdate(float diff)
    {
        var ticks = _periodicTicker.ConsumeTicks(diff, 1000f, true, 1, 4);
        if (ticks != 1) return;
        _damage = (75f + 40f * (_spell.CastInfo.SpellLevel - 1)) / 4 +
                  _owner.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        _unit.TakeDamage(_owner, _damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLPERSIST,
            false);
    }

    public void OnDeath(DeathData data)
    {
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_p);
        RemoveParticle(_p2);
    }
}