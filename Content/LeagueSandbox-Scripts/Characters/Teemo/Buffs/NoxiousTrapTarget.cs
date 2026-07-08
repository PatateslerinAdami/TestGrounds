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

public class BantamTrap : IBuffGameScript {
    private ObjAIBase      _teemo;
    private AttackableUnit _unit;
    private Buff           _buff;
    private Particle       _globalPoison;
    private PeriodicTicker _periodicTicker;
    private float          _damage;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit  = unit;
        _teemo = buff.SourceUnit;
        _buff  = buff;
        if (!_unit.IsDead) {
            _globalPoison = AddParticleTarget(_teemo, unit, "Global_Poison", unit, 4f, bone: "head");
            AddParticleTarget(_teemo, _unit, "ShroomMine", _unit);
        }

        ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
    }

    public void OnUpdate(Buff buff, float diff) {
        if (_unit.IsDead) {
            _buff.DeactivateBuff();
            return;
        }

        var ticks = _periodicTicker.ConsumeTicks(diff, 1000f, false, 1);
        if (ticks != 1) return;
        var trapSpell = _teemo.GetSpell("BantamTrap");
        _damage = 50f + 125f * (trapSpell.CastInfo.SpellLevel - 1) +
                  _teemo.Stats.AbilityPower.Total *
                  0.5f; //here because if ap increases due to effects this damage will increase
        _unit.TakeDamage(_teemo, _damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PERIODIC,
                         false);
    }

    private void OnDeath(DeathData data) { _buff.DeactivateBuff(); }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_globalPoison);
        ApiEventManager.OnDeath.RemoveListener(this, unit, OnDeath);
    }
}