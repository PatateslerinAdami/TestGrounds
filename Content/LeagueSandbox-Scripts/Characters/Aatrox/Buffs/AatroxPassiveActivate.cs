using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class AatroxPassiveActivate : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType= BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            if (unit.IsToRemove() || buff.TimeElapsed < buff.Duration)
            {
                return;
            }

            SetPARState((ObjAIBase)unit, 0);
            AddBuff("AatroxPassiveReady", 25000, 1, spell, unit, (ObjAIBase)unit, true);
        }
    }
}
