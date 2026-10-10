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
    public class AatroxWONHBuff : IBuffGameScript
    {
        private ObjAIBase _owner;
        private Spell _spell;
        private int _hits;
        private Attack _pending;
        private Buff _indicator;

        private sealed record Attack(AttackableUnit Target, bool Power, float Heal, float Damage, float Cost);

        public bool Ready
        {
            get
            {
                return _hits == 2;
            }
        }

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COUNTER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            UpdateInfinite = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            _spell = spell;
            ApiEventManager.OnHitUnit.AddListener(this, _owner, OnHit);
        }

        private Attack Capture(AttackableUnit target)
        {
            int rank = _owner.Spells[1].CastInfo.SpellLevel;
            float bonusAD = _owner.Stats.AttackDamage.Total - _owner.Stats.AttackDamage.BaseValue;
            float damage = 60 + 35 * (rank - 1) + bonusAD;
            return new Attack(target, _owner.HasBuff("AatroxWPower"), 20 + 5 * (rank - 1) + 0.25f * bonusAD, damage, damage * 0.25f);
        }

        public void PrepareAttack(AttackableUnit target)
        {
            _pending = Capture(target);
        }

        public void OnUpdate(float diff)
        {
            var values = Capture(null);
            SetSpellToolTipVar(_owner, 4, values.Cost, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
            SetSpellToolTipVar(_owner, 5, values.Heal * 3, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
        }

        private void OnHit(DamageData data)
        {
            if (!data.IsAutoAttack)
            {
                return;
            }

            if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK)
            {
                return;
            }

            if (data.IsHitBlocked)
            {
                return;
            }

            if (data.Target == null)
            {
                return;
            }

            if (_owner.Spells[1].CastInfo.SpellLevel == 0)
            {
                return;
            }

            var attack = _pending != null && _pending.Target == data.Target ? _pending : Capture(data.Target);
            _pending = null;
            if (++_hits < 3)
            {
                RefreshIndicator();
                return;
            }

            _hits = 0;
            RefreshIndicator();
            if (attack.Power)
            {
                Spells.AatroxHelpers.PayHealth(_owner, attack.Cost);
                data.Target.TakeDamage(_owner, attack.Damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
                AddParticle(
                    _owner,
                    data.Target,
                    Spells.AatroxHelpers.Effect(_owner, "basic_hit_effect_03"),
                    data.Target.Position,
                    lifetime: 0.5f
                );
            }
            else
            {
                float heal = attack.Heal * (_owner.Stats.CurrentHealth < _owner.Stats.HealthPoints.Total * 0.5f ? 3 : 1);
                _owner.TakeHeal(_owner, heal, HealType.SelfHeal);
                AddParticleTarget(_owner, _owner, Spells.AatroxHelpers.Effect(_owner, "W_Life_Self"), _owner, 0.5f);
            }
        }

        public void RefreshIndicator()
        {
            if (_indicator != null && !_indicator.Remove)
            {
                RemoveBuff(_indicator);
            }

            _indicator = Ready ? AddBuff(_owner.HasBuff("AatroxWPower") ? "AatroxWONHPowerBuff" : "AatroxWONHLifeBuff", 25000, 1, _spell,
                _owner, _owner, true) : null;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            if (_indicator != null && !_indicator.Remove)
            {
                RemoveBuff(_indicator);
            }

            _pending = null;
        }
    }
}