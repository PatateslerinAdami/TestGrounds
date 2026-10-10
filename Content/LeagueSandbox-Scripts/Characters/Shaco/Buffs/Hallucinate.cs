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
    public class HallucinateVanish : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INVULNERABILITY,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            buff.SetStatusEffect(StatusFlags.Invulnerable, true);
            buff.SetStatusEffect(StatusFlags.NoRender, true);
            buff.SetStatusEffect(StatusFlags.Targetable, false);
            buff.SetStatusEffect(StatusFlags.CanMove, false);
            buff.SetStatusEffect(StatusFlags.CanAttack, false);
            buff.SetStatusEffect(StatusFlags.CanCast, false);
            unit.StopMovement();
            StopTargetingUnit(unit);
        }
    }

    public class Hallucinate : IBuffGameScript
    {
        private Pet _clone;
        private Buff _buff;
        private float _cooldown;
        private HallucinatePet _petBuff;
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (unit is Pet)
            {
                buff.Hidden = true;
                _petBuff = new HallucinatePet();
                _petBuff.OnActivate(unit, buff, ownerSpell);
                return;
            }
            var owner = (ObjAIBase)unit;
            _clone = owner.GetPet();
            _buff = buff;
            ApiEventManager.OnDeath.AddListener(this, unit, OnOwnerDeath);
            _cooldown = ownerSpell.CurrentCooldown;
            var guide = SetSpell(owner, "HallucinateGuide", SpellSlotType.SpellSlots, 3);
            guide.SetCooldown(0, true);
        }

        private void OnOwnerDeath(DeathData data)
        {
            _buff.DeactivateBuff();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_petBuff != null)
            {
                _petBuff.OnDeactivate(unit, buff, ownerSpell);
                ApiEventManager.RemoveAllListenersForOwner(_petBuff);
                return;
            }
            ApiEventManager.OnDeath.RemoveListener(this);
            var owner = (ObjAIBase)unit;
            if (_clone != null && !_clone.IsToRemove())
            {
                _clone.GetBuffWithName("Hallucinate")?.DeactivateBuff();
            }
            var restored = SetSpell(owner, "Hallucinate", SpellSlotType.SpellSlots, 3);
            restored.SetCooldown(Math.Max(0, _cooldown - buff.TimeElapsed), true);
        }
        public void OnUpdate(float diff)
        {
            _petBuff?.OnUpdate(diff);
        }
    }

    internal class HallucinatePet : IBuffGameScript
    {
        private Pet _clone;
        private Buff _buff;
        private Spell _spell;
        private float _damage;
        private Particle _particle;
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _clone = (Pet)unit;
            _buff = buff;
            _spell = ownerSpell;
            _damage = ownerSpell.SpellData.GetEffectAmount(1, ownerSpell.CastInfo.SpellLevel)
                + _clone.Owner.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
            ApiEventManager.OnPreDealDamage.AddListener(this, unit, OnDealDamage);
            ApiEventManager.OnPreTakeDamage.AddListener(this, unit, OnTakeDamage);
            ApiEventManager.OnHitUnit.AddListener(this, _clone, OnAttack);
            ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
            _particle = AddParticleTarget(_clone.Owner, unit, "Jester_Copy", unit, 18, teamOnly: _clone.Team);
        }

        private void OnDealDamage(DamageData data)
        {
            if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
            data.Damage *= 0.75f;
            data.PostMitigationDamage *= 0.75f;
        }

        private void OnTakeDamage(DamageData data)
        {
            if (data.DamageSource == DamageSource.DAMAGE_SOURCE_INTERNALRAW) return;
            float multiplier = 1 + _spell.SpellData.GetEffectAmount(2, _spell.CastInfo.SpellLevel) / 100;
            data.Damage *= multiplier;
            data.PostMitigationDamage *= multiplier;
        }

        private void OnAttack(DamageData data)
        {
            Spells.TwoShivPoison.ApplyPassive(_clone.Owner.Spells[2], _clone, data);
        }

        private void OnDeath(DeathData data)
        {
            _buff.DeactivateBuff();
        }

        public void OnUpdate(float diff)
        {
            if (_clone.IsDead || _clone.IsToRemove()) _buff.DeactivateBuff();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnPreDealDamage.RemoveListener(this);
            ApiEventManager.OnPreTakeDamage.RemoveListener(this);
            ApiEventManager.OnHitUnit.RemoveListener(this);
            ApiEventManager.OnDeath.RemoveListener(this);
            _particle?.SetToRemove();
            AddParticle(_clone.Owner, null, "Hallucinate_nova", _clone.Position, lifetime: 2, ignoreCasterVisibility: true);
            foreach (var enemy in GetUnitsInRange(_clone.Owner, _clone.Position, 250, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions))
            {
                enemy.TakeDamage(_clone.Owner, _damage, DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            }
            DespawnMinion(_clone);
            if (_clone.Owner.GetPet() == _clone) _clone.Owner.SetPet(null);
            RemoveBuff(_clone.Owner, "Hallucinate");
        }
    }
}
