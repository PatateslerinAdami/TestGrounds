using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Talents
{
    internal class Talent_4162 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            ApiEventManager.OnPreDealDamage.AddListener(this, owner, OnPreDealDamage);
        }
        
        private void OnPreDealDamage(DamageData data)
        {
            data.PostMitigationDamage += data.PostMitigationDamage * 0.03f;
        }
    }
}