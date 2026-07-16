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

public class JaxEmpowerTwo  : IBuffGameScript {
    private ObjAIBase _jax;
    private Spell     _spell;
    private Buff      _buff;
    private Particle  _p1, _p2;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _buff                                      = buff;
        _jax                                       = buff.SourceUnit;
        _spell = ownerspell;
        ownerspell.SetCooldown(0f);
        SealSpellSlot(_jax, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
        _p1 = SpellEffectCreate("armsmaster_empower_buf.troy",_jax, _jax,  _jax, lifetime: -1f, boneName: "Buffbone_Cstm_Weapon_1", flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
        _p2 = SpellEffectCreate("armsmaster_empower_self_01.troy",_jax, _jax,  _jax, lifetime: -1f, boneName: "Buffbone_Cstm_Weapon_1", flags: FXFlags.SimulateWhileOffScreen, targetBoneName: "weapon", fowVisibilityRadius: 10f);
        ApiEventManager.OnHitUnit.AddListener(this, _jax, OnHit);
        OverrideAutoAttack(_jax, "JaxEmpowerAttack", true);
    }

    private void OnHit(DamageData data) {
        if (!IsValidTarget(_jax, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        var ap  = _jax.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var dmg = _spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] + ap;
        SpellEffectCreate("EmpowerTwoHit_tar.troy",_jax, data.Target,  data.Target, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
        data.Target.TakeDamage(_jax, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
                               DamageResultType.RESULT_NORMAL);
        RemoveBuff(_buff);
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        ApiEventManager.OnHitUnit.RemoveListener(this, _jax, OnHit);
        RemoveOverrideAutoAttack(_jax);
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        SealSpellSlot(_jax, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
        spell.SetCooldown(spell.GetCooldown(), false);
    }
    
}