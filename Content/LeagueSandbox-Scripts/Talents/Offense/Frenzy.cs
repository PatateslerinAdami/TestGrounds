using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Talents
{
    internal class Talent_4151 : ITalentScript
    {
        private ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _owner = owner;
            ApiEventManager.OnDealDamage.AddListener(this, owner, OnDealDamage);
        }
        
        private void OnDealDamage(DamageData data)
        {
            if (data.DamageResultType is DamageResultType.RESULT_CRITICAL)
            {
                AddBuff("Mastery_Fervor", 3f, 1, _owner.AutoAttackSpell, _owner, _owner);
            }
        }
    }
}