using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4142 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            int level = rank;
            var statsModifier = new StatsModifier();
            statsModifier.AttackDamage.PercentBonus = 0.02f + 0.15f * (level - 1);
            owner.AddStatModifier(statsModifier);
        }
    }
}