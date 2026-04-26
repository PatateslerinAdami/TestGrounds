using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class UdyrPhoenixActivation : IBuffGameScript {
    private ObjAIBase _udyr;
    private Spell     _spell;
    private float     _damageTimer = 0f;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.DAMAGE,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _udyr  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        AddParticleTarget(_udyr, _udyr, "PhoenixStance",      _udyr, buff.Duration);
    }

    public void OnUpdate(float diff) {
        _damageTimer -= diff;
        if (_damageTimer > 0f) return;
        AddParticleTarget(_udyr, _udyr, "Udyr_Phoenix_nova", _udyr);
        var dmg = 15f + 10 * (_spell.CastInfo.SpellLevel = 1) + _udyr.Stats.AbilityPower.Total * 0.25f; 
        var targets = GetUnitsInRange(_udyr, _udyr.Position, 250f, true, SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var unit in targets) {
            unit.TakeDamage(_udyr, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                            DamageResultType.RESULT_NORMAL);
        }
        _damageTimer = 1000f;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}