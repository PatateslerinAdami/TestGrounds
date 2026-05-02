using System;
using System.Linq;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.API;

namespace CharScripts
{
    public class CharScriptYasuo : ICharScript 
    {
        private ObjAIBase _yasuo;

        public void OnActivate(ObjAIBase owner, Spell spell = null)
        {
            _yasuo = owner;      
            AddBuff("YasuoCritMod", float.MaxValue, 1, spell, owner, owner, true);
            AddBuff("YasuoPassive", float.MaxValue, 1, spell, owner, owner, true); 
            AddBuff("YasuoRAvailableTest", float.MaxValue, 1, spell, owner, owner, true);

            ApiEventManager.OnLevelUp.AddListener(this, owner, OnLevelUp, false);
            UpdateFlowCapacity();
            _yasuo.Stats.CurrentMana = _yasuo.Stats.ManaPoints.Total;
        }

        private void OnLevelUp(AttackableUnit unit)
        {
            bool wasFull = _yasuo.Stats.CurrentMana >= _yasuo.Stats.ManaPoints.Total;
            UpdateFlowCapacity();
            if (wasFull)
            {
                _yasuo.Stats.CurrentMana = _yasuo.Stats.ManaPoints.Total;
            }
        }

        private void UpdateFlowCapacity()
        {
            float[] flowMax = { 60, 65, 70, 75, 80, 90, 100, 110, 125, 140, 160, 185, 215, 250, 290, 340, 400, 470 };
            int level = _yasuo.Stats.Level;
            if (level > 18) level = 18;
            _yasuo.Stats.ManaPoints.BaseValue = flowMax[level - 1];
        }
    }
}