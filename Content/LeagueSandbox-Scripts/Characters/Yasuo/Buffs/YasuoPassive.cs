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
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData { BuffType = BuffType.INTERNAL };
        public StatsModifier StatsModifier { get; set; } = new StatsModifier();
        private Vector2 _lastPos;
        private bool _isParStateActive = false; 

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _lastPos = unit.Position;
            ApiEventManager.OnUpdateStats.AddListener(this, unit, OnUpdate, false);
            ApiEventManager.OnTakeDamage.AddListener(this, unit, OnTakeDamage, false);
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
            
            if (!yasuo.HasBuff("YasuoPassiveMovementShield"))
            {
                AddBuff("YasuoPassiveMovementShield", float.MaxValue, 1, null, yasuo, yasuo, false);
            }

            var hud = unit.GetBuffWithName("YasuoPassiveMovementShield");
            if (hud != null)
            {
                hud.SetStacks((byte)Math.Clamp(stacks, 1, 100));
            }
        }

        public void OnTakeDamage(DamageData damageData)
        {
            var yasuo = damageData.Target as ObjAIBase;
            if (yasuo != null && yasuo.Stats.CurrentMana >= (yasuo.Stats.ManaPoints.Total - 1))
            {
                yasuo.Stats.CurrentMana = 0;
                SetPARState(yasuo, 0); 
                _isParStateActive = false;
                
                AddBuff("YasuoPassiveMSShieldOn", 1.0f, 1, null, yasuo, yasuo);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnUpdateStats.RemoveListener(this);
            ApiEventManager.OnTakeDamage.RemoveListener(this);
        }
    }
}