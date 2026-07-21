using System;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoPassive : IBuffGameScript
    {
        // Hidden carrier hosting the flow logic (replay: type AURA, IsHidden, permanent,
        // never updated, the visible flow meter lives on YasuoPassiveMSCharge).
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData {
            PersistsThroughDeath = true, BuffType = BuffType.AURA, IsHidden = true };
        public StatsModifier StatsModifier { get; set; } = new StatsModifier();

        private Vector2 _lastPos;
        private bool _isParStateActive = false;
        private int _lastFlowCounter = -1;

        private float _lastHealth;
        private float _lastMaxHealth;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _lastPos = unit.Position;
            _lastHealth = unit.Stats.CurrentHealth; 
            _lastMaxHealth = unit.Stats.HealthPoints.Total; 

            ApiEventManager.OnUpdateStats.AddListener(this, unit, OnUpdate, false);
            
            SetPARState(unit, 0); 
            _isParStateActive = false;
        }

        private void OnUpdate(AttackableUnit unit, float diff)
        {
            var yasuo = unit as ObjAIBase;
            if (yasuo == null) return;
            float dist = Vector2.Distance(unit.Position, _lastPos);
            _lastPos = unit.Position;
            float gain = (dist / 5000f) * yasuo.Stats.ManaPoints.Total;
            yasuo.Stats.CurrentMana = Math.Min(yasuo.Stats.CurrentMana + gain, yasuo.Stats.ManaPoints.Total);
            
            int stacks = (int)((yasuo.Stats.CurrentMana / yasuo.Stats.ManaPoints.Total) * 100);
            
            if (stacks >= 100 && !_isParStateActive)
            {
                SetPARState(yasuo, 1); 
                _isParStateActive = true;
            }
            else if (stacks < 100 && _isParStateActive)
            {
                SetPARState(yasuo, 0); 
                _isParStateActive = false;
            }
            
            UpdateFlowCounter(yasuo, stacks);

            float currentHealth = yasuo.Stats.CurrentHealth;
            float currentMaxHealth = yasuo.Stats.HealthPoints.Total;
            if (currentHealth < _lastHealth && currentMaxHealth >= _lastMaxHealth)
            {
                if (yasuo.Stats.CurrentMana >= (yasuo.Stats.ManaPoints.Total - 1))
                {
                    yasuo.Stats.CurrentMana = 0;
                    SetPARState(yasuo, 0);
                    _isParStateActive = false;
                    UpdateFlowCounter(yasuo, 0);

                    AddBuff("YasuoPassiveMSShieldOn", 1.0f, 1, null, yasuo, yasuo);
                }
            }
            _lastHealth = currentHealth;
            _lastMaxHealth = currentMaxHealth;
        }

        // Flow meter uses NPC_BuffUpdateNumCounter so use edit buff
        private void UpdateFlowCounter(ObjAIBase yasuo, int stacks)
        {
            stacks = Math.Clamp(stacks, 0, 100);
            if (stacks == _lastFlowCounter)
            {
                return;
            }

            var charge = yasuo.GetBuffWithName("YasuoPassiveMSCharge");
            if (charge == null)
            {
                return;
            }

            EditBuff(charge, stacks);
            _lastFlowCounter = stacks;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnUpdateStats.RemoveListener(this);
        }
    }
}