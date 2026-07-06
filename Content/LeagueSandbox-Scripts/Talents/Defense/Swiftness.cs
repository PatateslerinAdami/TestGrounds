using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4242 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            var healthModifier = new StatsModifier();
            healthModifier.SlowResistPercent = 0.1f;
            owner.AddStatModifier(healthModifier);
        }
    }
}