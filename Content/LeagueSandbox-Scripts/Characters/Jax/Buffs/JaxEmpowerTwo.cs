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
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks   = 6
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _buff                                      = buff;
        _jax                                       = ownerspell.CastInfo.Owner;
        _spell = ownerspell;
        SealSpellSlot(_jax, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
        ownerspell.SetCooldown(0f);
        _p1 = AddParticleTarget(_jax, _jax, "armsmaster_empower_buf", _jax, -1f, bone: "R_hand");
        _p2 = AddParticleTarget(_jax, _jax, "armsmaster_empower_self_01", _jax, -1f, bone: "R_hand", targetBone: "weapon");
        ApiEventManager.OnHitUnit.AddListener(this, _jax, OnHit);
        _jax.SetAutoAttackSpell("JaxEmpowerAttack", true);
    }

    private void OnHit(DamageData data) {
        if (!IsValidTarget(_jax, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        var ap  = _jax.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var dmg = 40f + 35f * (_spell.CastInfo.SpellLevel - 1) + ap;
        AddParticleTarget(_jax, data.Target, "EmpowerTwoHit_tar", data.Target);
        data.Target.TakeDamage(_jax, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
                               DamageResultType.RESULT_NORMAL);
        _buff.DeactivateBuff();
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        _jax.ResetAutoAttackSpell();
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        SealSpellSlot(_jax, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, false);
        spell.SetCooldown(spell.CastInfo.Cooldown, false);
    }
    
}