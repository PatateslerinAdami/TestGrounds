using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4112 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            int level = rank;
            var statsModifier = new StatsModifier();
            statsModifier.AttackSpeed.PercentBonus = 0.08f * level;
            owner.AddStatModifier(statsModifier);
        }
    }
}