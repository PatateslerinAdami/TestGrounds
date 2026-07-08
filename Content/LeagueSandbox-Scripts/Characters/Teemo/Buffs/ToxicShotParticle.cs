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

public class ToxicShotParticle : IBuffGameScript
{
    private ObjAIBase _teemo;
    private AttackableUnit _unit;
    private Particle _toxicShotTargetParticle;
    private Particle _globalPoisonParticle;
    private Buff _buff;
    private PeriodicTicker _periodicTicker;
    private float _damage;


    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.POISON,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _teemo = ownerSpell.CastInfo.Owner;
        _unit = unit;
        _buff = buff;
        _toxicShotTargetParticle = AddParticleTarget(_teemo, unit, "Toxicshot_tar", unit, 4f,
            size: unit.CharData.GameplayCollisionRadius * 0.025f);
        _globalPoisonParticle = AddParticleTarget(_teemo, unit, "Global_Poison", unit, 4f, bone: "head");
        ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
    }

    public void OnUpdate(Buff buff, float diff)
    {
        var ticks = _periodicTicker.ConsumeTicks(diff, 1000f, false);
        if (ticks != 1) return;
        _damage = 6f + 6 * (_teemo.GetSpell("ToxicShot").CastInfo.SpellLevel - 1) +
                  _teemo.Stats.AbilityPower.Total * 0.3f;
        _unit.TakeDamage(_teemo, _damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PERIODIC,
            false);
    }

    private void OnDeath(DeathData data)
    {
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_globalPoisonParticle);
        RemoveParticle(_toxicShotTargetParticle);
    }
}