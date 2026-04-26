using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class MalphiteCleave : IBuffGameScript {
    private ObjAIBase        _malphite;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _malphite = ownerSpell.CastInfo.Owner;
        ApiEventManager.OnHitUnit.AddListener(this, _malphite, OnHit);
        
    }
    
    public void OnHit(DamageData data) {
        AddParticleTarget(_malphite, data.Target,
                          _malphite.HasBuff("ObduracyBuff") ? "MalphiteCleaveEnragedHit" : "MalphiteCleaveHit",
                          data.Target);
        var units = GetUnitsInRange(_malphite,_malphite.Position, 325, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var unit in units.Where(unit => unit.Team != _malphite.Team && unit != data.Target)) {
            unit.TakeDamage(_malphite, _malphite.Stats.AttackDamage.Total * 0.3f, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PROC, DamageResultType.RESULT_NORMAL);
            AddParticleTarget(_malphite, unit,
                              _malphite.HasBuff("ObduracyBuff") ? "MalphiteCleaveEnragedHit" : "MalphiteCleaveHit",
                              unit);
        }
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}