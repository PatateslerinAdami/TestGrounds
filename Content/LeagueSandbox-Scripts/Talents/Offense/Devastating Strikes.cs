using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;


namespace Talents
{
    internal class Talent_4152 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            var statsModifier = new StatsModifier();
            statsModifier.ArmorPenetration.PercentBaseBonus = 0.02f * rank;
            statsModifier.MagicPenetration.PercentBaseBonus = 0.02f * rank;
            owner.AddStatModifier(statsModifier);
        }
    }
}