using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4152 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            var modifier = new StatsModifier();
            float penetration = 0.02f * rank;
            modifier.ArmorPenetration.PercentBaseBonus = penetration;
            modifier.MagicPenetration.PercentBaseBonus = penetration;
            owner.AddStatModifier(modifier);
        }
    }
}