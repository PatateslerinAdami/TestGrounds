using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4112 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            var modifier = new StatsModifier();
            modifier.AttackSpeed.PercentBonus = 0.0125f * rank;
            owner.AddStatModifier(modifier);
        }
    }
}