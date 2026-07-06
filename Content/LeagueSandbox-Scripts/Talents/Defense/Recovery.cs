using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4212 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            var healthModifier = new StatsModifier();
            healthModifier.HealthRegeneration.PercentBonus = 0.01f * rank;
            owner.AddStatModifier(healthModifier);
        }
    }
}