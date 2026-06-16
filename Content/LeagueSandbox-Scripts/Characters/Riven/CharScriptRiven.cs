using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts
{
    public class CharScriptRiven : ICharScript
    {
        public StatsModifier StatsModifier { get; } = new();

        public void OnActivate(ObjAIBase owner, Spell spell = null)
        {
            AddBuff("APBonusDamageToTowers", 25000f, 1, spell, owner, owner, infiniteduration: true);
            AddBuff("ChampionChampionDelta", 25000f, 1, spell, owner, owner, infiniteduration: true);
            AddBuff("RivenPassiveWatcher", 25000f, 1, spell, owner, owner, infiniteduration: true);

            ApiEventManager.OnLevelUp.AddListener(this, owner, OnLevelUp);
        }

        public void OnPostActivate(ObjAIBase owner, Spell spell = null)
        {
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell = null)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }

        public void OnUpdate(float diff)
        {
        }

        private void OnLevelUp(AttackableUnit unit)
        {
            if (unit is ObjAIBase owner)
            {
                var buff = owner.GetBuffWithName("RivenPassiveAABoost");
                if (buff != null)
                {
                    int level = owner.Stats.Level;
                    int[] baseDamage = { 5, 5, 5, 7, 7, 7, 9, 9, 9, 11, 11, 11, 13, 13, 13, 15, 15, 15, 15 };
                    int index = System.Math.Min(level - 1, baseDamage.Length - 1);
                    buff.SetToolTipVar(1, (float)baseDamage[index]);
                }
            }
        }
    }
}
