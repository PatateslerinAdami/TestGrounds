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

namespace Buffs
{
    public class OdinCenterRelic : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Buff _buff;
        private Shield _shield;
        private Particle _particle;
        private float _rechargeRemaining; 
        private float _maximumShield;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            RestoreShield();
            ApiEventManager.OnTakeDamage.AddListener(this, unit, OnDamage);
            ApiEventManager.OnDeath.AddListener(this, unit, data => buff.DeactivateBuff());
        }

        private void RestoreShield()
        {
            _maximumShield = 212.5f + 12.5f * _buff.TargetUnit.Stats.Level;
            if (_shield != null)
            {
                ApiEventManager.OnShieldBreak.RemoveListener(this);
                RemoveShield(_buff.TargetUnit, _shield);
            }

            _shield = AddShield(_buff.SourceUnit, _buff.TargetUnit, _maximumShield);
            ApiEventManager.OnShieldBreak.AddListener(this, _shield, shield => RemoveShieldParticle());
            if (_particle == null)
            {
                _particle = AddParticleTarget(
                    _buff.SourceUnit, _buff.TargetUnit, "odin_center_relic.troy", _buff.TargetUnit,
                    _buff.Duration - _buff.TimeElapsed, bone: "root", ignoreCasterVisibility: true
                );
            }

            _rechargeRemaining = 0;
            UpdateTooltip();
        }

        private void OnDamage(DamageData data)
        {
            if (data.Damage > 0 && data.Attacker != _buff.TargetUnit
                && data.DamageResultType != DamageResultType.RESULT_DODGE
                && data.DamageResultType != DamageResultType.RESULT_MISS)
            {
                _rechargeRemaining = 10000f;
                UpdateTooltip();
            }
        }

        private void UpdateTooltip()
        {
            float remaining = _shield?.Amount ?? 0;
            if (_buff.ToolTipData.Values[0].Value != remaining)
            {
                _buff.SetToolTipVar(0, remaining);
            }
        }

        private void RemoveShieldParticle()
        {
            RemoveParticle(_particle);
            _particle = null;
        }

        public void OnUpdate(float diff)
        {
            if (_buff.Elapsed())
            {
                return;
            }

            if (_buff.TargetUnit.IsDead)
            {
                _buff.DeactivateBuff();
                return;
            }

            if (_rechargeRemaining > 0)
            {
                _rechargeRemaining -= diff;
                if (_rechargeRemaining <= 0)
                {
                    RestoreShield();
                }
            }

            UpdateTooltip();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveShield(unit, _shield);
            _shield = null;
            RemoveShieldParticle();
        }
    }

    public class OdinCenterRelicDamage : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Buff _buff;
        private Particle _particle;
        private float _cooldown; 

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            _particle = AddParticleTarget(
                unit, unit, "odin_relic_buf_light_blue.troy", unit, buff.Duration,
                bone: "root", ignoreCasterVisibility: true
            );
            ApiEventManager.OnDealDamage.AddListener(this, unit, OnDamage);
            ApiEventManager.OnDeath.AddListener(this, unit, data => buff.DeactivateBuff());
        }

        private void OnDamage(DamageData data)
        {
            if (_buff.Elapsed() || _buff.TargetUnit.IsDead || _cooldown > 0
                || data.Attacker != _buff.TargetUnit || data.Damage <= 0
                || !(data.Target is ObjAIBase target) || target is BaseTurret
                || target is Minion { IsWard: true }
                || target.IsDead || target.IsToRemove() || target.Team == _buff.TargetUnit.Team
                || !target.Status.HasFlag(StatusFlags.Targetable)
                || !target.CanTakeDamage(DamageType.DAMAGE_TYPE_MAGICAL)
                || data.DamageResultType == DamageResultType.RESULT_DODGE
                || data.DamageResultType == DamageResultType.RESULT_MISS)
            {
                return;
            }

            switch (data.DamageSource)
            {
                case DamageSource.DAMAGE_SOURCE_ATTACK:
                case DamageSource.DAMAGE_SOURCE_SPELL:
                case DamageSource.DAMAGE_SOURCE_SPELLAOE:
                case DamageSource.DAMAGE_SOURCE_SPELLPERSIST:
                case DamageSource.DAMAGE_SOURCE_PERIODIC:
                    break;
                default:
                    return;
            }

            _cooldown = 4000f;
            AddParticleTarget(
                _buff.TargetUnit, _buff.TargetUnit, "odin_centerbuffbeam.troy", target, 0.5f,
                bone: "root", targetBone: "root", ignoreCasterVisibility: true
            );
            target.TakeDamage(
                _buff.TargetUnit, 90f + 10f * _buff.TargetUnit.Stats.Level,
                DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, false
            );
        }

        public void OnUpdate(float diff)
        {
            _cooldown = Math.Max(0, _cooldown - diff);
            if (_buff.TargetUnit.IsDead)
            {
                _buff.DeactivateBuff();
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveParticle(_particle);
            _particle = null;
        }
    }
}
