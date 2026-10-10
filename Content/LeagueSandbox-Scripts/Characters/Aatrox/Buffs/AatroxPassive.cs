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
    public class AatroxPassive : IBuffGameScript
    {
        private ObjAIBase _owner;
        private float _combatDelay, _decayTime;
        private bool _applied;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType= BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            UpdateInfinite = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            RefreshAttackSpeed();
            ApiEventManager.OnTakeDamage.AddListener(this, _owner, Combat);
            ApiEventManager.OnDealDamage.AddListener(this, _owner, Combat);
        }

        private void Combat(DamageData data)
        {
            if (data.Attacker != data.Target && data.PostMitigationDamage > 0)
            {
                _combatDelay = 6000;
                _decayTime = 0;
            }
        }

        public void OnUpdate(float diff)
        {
            if (_owner.IsDead || _owner.HasBuff("AatroxPassiveDeath"))
            {
                return;
            }

            RefreshAttackSpeed();
            float outOfCombat = Math.Max(0, diff - Math.Max(0, _combatDelay));
            _combatDelay = Math.Max(0, _combatDelay - diff);
            _decayTime += outOfCombat;
            while (_decayTime >= 1000)
            {
                _owner.SpendPAR(_owner.GetMaxPAR() * 0.02f);
                _decayTime -= 1000;
            }

            RefreshAttackSpeed();
        }

        public void RefreshAttackSpeed()
        {
            float bonus = Math.Clamp(_owner.GetPAR() / Math.Max(1, _owner.GetMaxPAR()), 0, 1) * 0.5f;
            if (_applied && StatsModifier.AttackSpeed.FlatBonus == bonus)
            {
                return;
            }

            if (_applied)
            {
                _owner.RemoveStatModifier(StatsModifier);
            }

            StatsModifier.AttackSpeed.FlatBonus = bonus;
            _owner.AddStatModifier(StatsModifier);
            _applied = true;
        }

        public void AddBlood(float amount)
        {
            IncreasePAR(_owner, amount, PrimaryAbilityResourceType.BloodWell);
            _combatDelay = Math.Max(_combatDelay, 1500);
            RefreshAttackSpeed();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}