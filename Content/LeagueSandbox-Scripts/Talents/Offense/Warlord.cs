using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4142 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            var modifier = new StatsModifier();
            float percent = rank switch
            {
                1 => 0.02f,
                2 => 0.035f,
                3 => 0.05f,
                _ => 0f
            };
            modifier.AttackDamage.PercentBonus = percent;
            owner.AddStatModifier(modifier);
        }
    }
}