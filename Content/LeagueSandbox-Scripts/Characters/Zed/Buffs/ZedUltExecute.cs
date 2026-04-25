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
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class ZedUltExecute : IBuffGameScript {
    private ObjAIBase      _zed;
    private Spell          _spell;
    private AttackableUnit _unit;
    private Buff           _buff;
    private Particle       _killIndicator, _delayedDamageIndicator;
    private float          _damage = 0f;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff   = buff;
        _unit   = unit;
        _zed    = ownerSpell.CastInfo.Owner;
        _spell  = ownerSpell;
        _damage = _zed.Stats.AttackDamage.Total;
        _zed.SetStatus(StatusFlags.Ghosted, true);
        AddParticleTarget(_zed, unit, "Zed_Ult_Impact",             unit);
        _delayedDamageIndicator ??= AddParticleTarget(_zed, _unit, "Zed_Ult_DelayedDamage_tar", _unit, buff.Duration);
        ApiEventManager.OnTakeDamage.AddListener(this, unit, OnTakeDamage);
        ApiEventManager.OnDeath.AddListener(this, unit, OnDeath, true);
    }

    public void OnUpdate(float diff) {
        if (_unit.Stats.GetPostMitigationDamage(_damage, DamageType.DAMAGE_TYPE_PHYSICAL, _zed) >
            _unit.Stats.CurrentHealth && _killIndicator == null) {
            _killIndicator = AddParticleTarget(_zed, _unit, "Zed_Base_R_buf_tell", _unit, _buff.Duration - _buff.TimeElapsed);
        } else if (_unit.Stats.GetPostMitigationDamage(_damage, DamageType.DAMAGE_TYPE_PHYSICAL, _zed) <
                   _unit.Stats.CurrentHealth && _killIndicator != null) { RemoveParticle(_killIndicator); }
    }

    private void OnTakeDamage(DamageData data) {
        if (_unit.IsDead) {
            _buff.DeactivateBuff();
            return;
        }

        if (!IsZedOrShadowSource(data.Attacker)) return;
        if (data.DamageType is not (DamageType.DAMAGE_TYPE_PHYSICAL or DamageType.DAMAGE_TYPE_MAGICAL)) return;

        var preMitigationDamage               = Math.Max(0f, data.Damage);
        if (preMitigationDamage > 0f) _damage += preMitigationDamage * 0.2f + 25f * (_spell.CastInfo.SpellLevel - 1);
    }

    private void OnDeath(DeathData data) {
        _buff?.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.OnTakeDamage.RemoveListener(this, unit, OnTakeDamage);
        ApiEventManager.OnDeath.RemoveListener(this, unit, OnDeath);

        if (_killIndicator != null) RemoveParticle(_killIndicator);
        if (_delayedDamageIndicator != null) RemoveParticle(_delayedDamageIndicator);
        if (!_zed.IsDead) _zed.SetStatus(StatusFlags.Ghosted, false);

        if (unit.IsDead) return;

        AddParticleTarget(_zed, unit, "Zed_Ult_DelayedDamage_proc", unit);
        AddParticleTarget(
            _zed, unit,
            _unit.Stats.GetPostMitigationDamage(_damage, DamageType.DAMAGE_TYPE_PHYSICAL, _zed) >
            _unit.Stats.CurrentHealth
                ? "zed_ult_pop_kill"
                : "zed_ult_pop_nokill", unit);
        _unit.TakeDamage(_zed, _damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                         DamageResultType.RESULT_NORMAL);

        if (_unit.Stats.GetPostMitigationDamage(_damage, DamageType.DAMAGE_TYPE_PHYSICAL, _zed) >
            _unit.Stats.CurrentHealth) { AddParticleTarget(_zed, _zed, "zed_ult_pop_kill_self_sound", _zed); }
    }

    private bool IsZedOrShadowSource(AttackableUnit attacker) {
        if (attacker == null) return false;
        if (attacker == _zed) return true;

        if (attacker is Minion shadow && shadow.Owner == _zed) {
            return shadow.Model == "ZedShadow" || shadow.Name is "ZedWShadow" or "ZedRShadow";
        }

        return false;
    }
}
