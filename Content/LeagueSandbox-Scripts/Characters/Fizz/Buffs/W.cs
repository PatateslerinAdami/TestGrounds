using System;
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
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class FizzTridentOnHit : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            IsHidden = true,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = (ObjAIBase)unit;
            ApiEventManager.OnHitUnit.AddListener(this, _owner, OnHit);
        }

        private void OnHit(DamageData data)
        {
            var spell = _owner.Spells[1];
            byte rank = spell.CastInfo.SpellLevel;
            var target = data.Target;
            if (spell.SpellName != "FizzSeastonePassive")
            {
                return;
            }

            if (rank == 0)
            {
                return;
            }

            if (target == null)
            {
                return;
            }

            if (target.IsDead)
            {
                return;
            }

            if (target.Team == _owner.Team)
            {
                return;
            }

            if (target is ObjBuilding)
            {
                return;
            }

            if (target is Minion { IsWard: true })
            {
                return;
            }

            var variables = new BuffVariables();
            variables.Set("base", spell.SpellData.GetEffectAmount(1, rank) + spell.SpellData.Coefficient * _owner.Stats.AbilityPower.Total);
            variables.Set("missing", spell.SpellData.GetEffectAmount(2, rank) / 100);
            float initial = variables.GetFloat("base") + variables.GetFloat("missing") * MathF.Max(0, target.Stats.HealthPoints.Total - target.Stats.CurrentHealth);
            if (target is Minion)
            {
                initial = MathF.Min(300, initial);
            }

            target.TakeDamage(_owner, initial / 12, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PERIODIC, false, spell);
            if (target.IsDead)
            {
                return;
            }

            AddBuff("FizzSeastoneTrident", 3.1f, 1, spell, target, _owner, buffVariables: variables);
            if (_owner.GetBuffWithName("FizzSeastonePassive") is { Remove: false, BuffScript: FizzSeastonePassive active })
            {
                target.TakeDamage(_owner, active.Damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, false, spell);
                AddBuff("GrievousWounds", 3, 1, spell, target, _owner);
                AddParticleTarget(_owner, target, "fizz_seastoneactive_hit_sound.troy", target, lifetime: 0.5f);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class FizzSeastonePassive : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();
        public float Damage { get; private set; }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            var owner = buff.SourceUnit;
            Damage = ownerSpell.SpellData.GetEffectAmount(5, ownerSpell.CastInfo.SpellLevel) + ownerSpell.SpellData.Coefficient2 * owner.Stats.AbilityPower.Total;
            ownerSpell.SetIconIndex(1);
            ApiEventManager.OnDeath.AddListener(this, unit, _ => RemoveBuff(buff));
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ownerSpell.SetIconIndex(0);
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class FizzSeastoneTrident : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Buff _buff;
        private float _elapsed;
        private int _ticks;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            ApiEventManager.OnDeath.AddListener(this, unit, _ => RemoveBuff(buff));
        }

        public void OnUpdate(float diff)
        {
            _elapsed += diff / 1000;
            while (_ticks < 6 && _elapsed >= (_ticks + 1) * 0.5f && !_buff.Remove)
            {
                _ticks++;
                var unit = _buff.TargetUnit;
                if (unit.IsDead || unit.IsToRemove())
                {
                    RemoveBuff(_buff);
                    return;
                }

                float total = _buff.Variables.GetFloat("base") + _buff.Variables.GetFloat("missing") * MathF.Max(0, unit.Stats.HealthPoints.Total - unit.Stats.CurrentHealth);
                if (unit is Minion)
                {
                    total = MathF.Min(300, total);
                }

                unit.TakeDamage(_buff.SourceUnit, total / 6, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PERIODIC, false,
                    _buff.OriginSpell);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
