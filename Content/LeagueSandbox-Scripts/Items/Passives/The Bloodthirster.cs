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
        private Particle _shieldParticle; // Itemul gestionează acum particula vizuală!
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

            RemoveShieldAndVisuals(false); // La vânzare, doar dispare, fără "explozia" de Proc
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

            // Dacă scutul a fost spart sau s-a topit complet, declanșăm animația de sfârșit (Proc)
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
                RemoveShieldAndVisuals(true); // Dacă a ajuns la 0, ștergem și afișăm Proc!
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

                    // Reînnoim cronometrul de 25s pe HUD
                    AddBuff("BloodthirsterDummySpell", 25f, 1, null, _owner, _owner);

                    // Adăugăm vizualul scutului DOAR dacă nu îl avem deja pe noi
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
                // Resetăm timer-ul de pe ecran la fiecare lovitură primită
                AddBuff("BloodthirsterDummySpell", 25f, 1, null, _owner, _owner);

                // Sincronizăm tracker-ul manual cu daunele
                float absorbed = Math.Min(_shieldAmount, data.PostMitigationDamage);
                _shieldAmount -= absorbed;

                // Dacă s-a spart, ștergem totul și apare Proc
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

                // Când timer-ul de 25s de pe HUD expiră
                if (!_owner.HasBuff("BloodthirsterDummySpell"))
                {
                    _decayTimer -= diff;
                    
                    if (_decayTimer <= 0)
                    {
                        // Scade scutul și păstrează scutul vizual
                        _shieldAmount -= 25f; 
                        _decayTimer = 100f; 
                        
                        UpdateNativeShield(); // Dacă ajunge la 0 în această funcție, va apela singur RemoveShieldAndVisuals!
                    }
                }
            }
        }
    }
}