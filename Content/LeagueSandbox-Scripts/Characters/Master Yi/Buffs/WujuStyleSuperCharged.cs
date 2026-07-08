using System;
using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class WujuStyleSuperCharged : IBuffGameScript {
    private ObjAIBase _masterYi;
    private Spell     _spell;
    private Buff      _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        IsNonDispellable = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _masterYi = buff.SourceUnit;
        _spell    = ownerSpell;
        _buff     = buff;
        ApiEventManager.OnHitUnit.AddListener(this, _masterYi, OnHit);
    }

    private void OnHit(DamageData data) {
        if (!IsValidTarget(_masterYi, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        var ad  = _masterYi.Stats.AttackDamage.Total * (_spell.SpellData.EffectLevelAmount[2][_spell.CastInfo.SpellLevel]/100);
        var dmg = _spell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel] + ad;
        data.Target.TakeDamage(_masterYi, dmg, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_PROC, DamageResultType.RESULT_NORMAL);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (ownerSpell.CurrentCooldown <= 0) return;
        RemoveBuff(_masterYi, "WujuStyle");
    }
}