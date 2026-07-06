using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Talents
{
    internal class Talent_4224 : ITalentScript
    {
        private ObjAIBase _owner;
        
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _owner = owner;
            ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamage);
        }

        private void OnTakeDamage(DamageData data)
        {
            if (data.Attacker is Monster)
            {
                AddBuff("MasteryOffenseBleed", 2f, 1, _owner.AutoAttackSpell, data.Attacker, _owner);
            }
        }
    }
}