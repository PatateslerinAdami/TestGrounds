using System;
using LeagueSandbox.GameServer.API;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3072 : IItemScript
    {
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private ObjAIBase _owner;
        private Shield _btShield;
        private Particle _shieldParticle;
        private float _shieldAmount = 0f;
        private float _decayTimer = 0f; 

        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            StatsModifier.LifeSteal.FlatBonus = 0.20f;
            _owner.AddStatModifier(StatsModifier);

            ApiEventManager.OnHitUnit.AddListener(this, owner, OnHitUnit, false);
            ApiEventManager.OnUpdateStats.AddListener(this, owner, OnUpdateStats, false);
            ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamage, false);
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.OnHitUnit.RemoveListener(this);
            ApiEventManager.OnUpdateStats.RemoveListener(this);
            ApiEventManager.OnTakeDamage.RemoveListener(this);
            _owner.RemoveStatModifier(StatsModifier);

            RemoveShieldAndVisuals(false); 
        }

        private void RemoveShieldAndVisuals(bool triggerProc)
        {
            _shieldAmount = 0f;

            if (_btShield != null)
            {
                _owner.RemoveShield(_btShield);
                _btShield = null;
            }

            if (_shieldParticle != null)
            {
                RemoveParticle(_shieldParticle);
                _shieldParticle = null;
            }
            
            var buff = _owner.GetBuffWithName("BloodthirsterDummySpell");
            if (buff != null) buff.DeactivateBuff();
            if (triggerProc)
            {
                AddParticleTarget(_owner, _owner, "Item_BTOverheal_Proc.troy", _owner, 1f, 1f, "C_BUFFBONE_GLB_CHEST_LOC");
            }
        }

        private void UpdateNativeShield()
        {
            if (_btShield != null) 
            {
                _owner.RemoveShield(_btShield);
                _btShield = null;
            }

            if (_shieldAmount <= 0)
            {
                RemoveShieldAndVisuals(true);
                return;
            }

            _btShield = new Shield(_owner, _owner, true, true, _shieldAmount);
            _owner.AddShield(_btShield);
        }

        private void OnHitUnit(DamageData data)
        {
            if (data.IsAutoAttack)
            {
                float lifeStealPercent = _owner.Stats.LifeSteal.Total;
                float healAmount = data.PostMitigationDamage * lifeStealPercent;
                float missingHp = _owner.Stats.HealthPoints.Total - _owner.Stats.CurrentHealth;

                if (healAmount > missingHp)
                {
                    float overheal = healAmount - missingHp;
                    if (missingHp <= 0) overheal = healAmount;

                    float maxShield = 50f + (_owner.Stats.Level - 1) * 17.64f;

                    if (_btShield != null && _btShield.IsConsumed())
                    {
                        _shieldAmount = 0f;
                    }
                    _shieldAmount += overheal;
                    if (_shieldAmount > maxShield) _shieldAmount = maxShield;
                    UpdateNativeShield();
                    AddBuff("BloodthirsterDummySpell", 25f, 1, null, _owner, _owner);
                    if (_shieldParticle == null)
                    {
                        _shieldParticle = AddParticleTarget(_owner, _owner, "Item_BTOverheal_Shield.troy", _owner, float.MaxValue, 1f, "C_BUFFBONE_GLB_CHEST_LOC");
                    }
                }
            }
        }

        private void OnTakeDamage(DamageData data)
        {
            if (_shieldAmount > 0)
            {
                AddBuff("BloodthirsterDummySpell", 25f, 1, null, _owner, _owner);
                float absorbed = Math.Min(_shieldAmount, data.PostMitigationDamage);
                _shieldAmount -= absorbed;
                if (_shieldAmount <= 0)
                {
                    RemoveShieldAndVisuals(true);
                }
            }
        }

        private void OnUpdateStats(AttackableUnit who, float diff)
        {
            if (_btShield != null)
            {
                if (_btShield.IsConsumed())
                {
                    RemoveShieldAndVisuals(true);
                    return;
                }

                if (!_owner.HasBuff("BloodthirsterDummySpell"))
                {
                    _decayTimer -= diff;
                    
                    if (_decayTimer <= 0)
                    {
                        _shieldAmount -= 10f; 
                        _decayTimer = 1f; 
                        UpdateNativeShield(); 
                    }
                }
            }
        }
    }
}