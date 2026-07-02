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
        
        // Reintroducem cronometrul pentru ritmul fix
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

            RemoveShieldAndBuffs(false); 
        }

        private void RemoveShieldAndBuffs(bool triggerProc = false)
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

            var timerBuff = _owner.GetBuffWithName("ItemBTOverhealTimer");
            if (timerBuff != null) timerBuff.DeactivateBuff();
            
            var decayBuff = _owner.GetBuffWithName("ItemBTOverhealDecay");
            if (decayBuff != null) decayBuff.DeactivateBuff();

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
                RemoveShieldAndBuffs(true);
                return;
            }

            _btShield = new Shield(_owner, _owner, true, true, _shieldAmount);
            _owner.AddShield(_btShield);
        }

        private void EnterCombatState()
        {
            var decayBuff = _owner.GetBuffWithName("ItemBTOverhealDecay");
            if (decayBuff != null) decayBuff.DeactivateBuff();

            AddBuff("ItemBTOverhealTimer", 25f, 1, null, _owner, _owner);
        }

        private void OnHitUnit(DamageData data)
        {
            if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
            float lifeStealPercent = _owner.Stats.LifeSteal.Total;
            float healAmount = data.PostMitigationDamage * lifeStealPercent;
            float missingHp = _owner.Stats.HealthPoints.Total - _owner.Stats.CurrentHealth;

            if (!(healAmount > missingHp)) return;
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
            EnterCombatState(); 

            if (_shieldParticle == null)
            {
                _shieldParticle = AddParticleTarget(_owner, _owner, "Item_BTOverheal_Shield.troy", _owner, float.MaxValue, 1f, "C_BUFFBONE_GLB_CHEST_LOC");
            }
        }

        private void OnTakeDamage(DamageData data)
        {
            if (_shieldAmount > 0)
            {
                EnterCombatState(); 
                
                float absorbed = Math.Min(_shieldAmount, data.PostMitigationDamage);
                _shieldAmount -= absorbed;
                
                if (_shieldAmount <= 0)
                {
                    RemoveShieldAndBuffs(true); 
                }
            }
        }

        private void OnUpdateStats(AttackableUnit who, float diff)
        {
            if (_btShield != null)
            {
                if (_btShield.IsConsumed())
                {
                    RemoveShieldAndBuffs(true);
                    return;
                }

                if (!_owner.HasBuff("ItemBTOverhealTimer"))
                {
                    if (!_owner.HasBuff("ItemBTOverhealDecay"))
                    {
                        AddBuff("ItemBTOverhealDecay", float.MaxValue, 1, null, _owner, _owner);
                    }
                    else
                    {
                        // Scădem timpul trecut din cronometrul nostru
                        _decayTimer -= diff;
                        
                        if (_decayTimer <= 0)
                        {
                            // Valoare Flat: Scădem fix 8.5 scut
                            _shieldAmount -= 8.5f; 
                            
                            // Resetăm cronometrul pentru următorul "puls" la fix 250 milisecunde
                            _decayTimer = 250f; 
                            
                            if (_shieldAmount <= 0)
                            {
                                RemoveShieldAndBuffs(true);
                            }
                            else
                            {
                                UpdateNativeShield(); 
                            }
                        }
                    }
                }
            }
        }
    }
}