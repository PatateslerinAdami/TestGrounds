using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4213 : ITalentScript
    {
        private byte _rank;
        
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            var statsModifier = new StatsModifier();
            statsModifier.Armor.FlatBonus = owner.Stats.Armor.FlatBonus * (2.5f * rank);
            statsModifier.MagicResist.FlatBonus = owner.Stats.MagicResist.FlatBonus * (2.5f * rank);
            owner.AddStatModifier(statsModifier);
        }
    }
}