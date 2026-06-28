using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace AIScripts
{
    // Faithful port of Scripts/BaronMinionAI.lua (4.20). It is the lane-minion AI (forward-nav push,
    // anti-kite, fear/flee/taunt) with three deliberate differences, all confirmed against the Lua
    // and the Averdrian AI420 C# ("100% identical to lua"):
    //   1. Target acquisition is filtered to AI_TARGET_MINIONS — it only ever acquires minions, never
    //      champions or structures (FindTargetInAcRWithFilter).
    //   2. OnCallForHelp is a no-op — it never answers an ally's call (HandlesCallsForHelp = false).
    //   3. OnCollisionEnemy is a no-op — body-contact with an enemy does not make it engage.
    // Everything else (the forward-nav push, anti-kite, fear/flee/taunt reevaluation) is inherited
    // from LaneMinionAI unchanged.
    public class BaronMinionAI : LaneMinionAI
    {
        public BaronMinionAI()
        {
            // BaronMinionAI.lua OnCallForHelp is empty — opt out of the call-for-help broadcast.
            AIScriptMetaData.HandlesCallsForHelp = false;
        }

        // FindTargetInAcRWithFilter(AI_TARGET_MINIONS): only units carrying the `Minion` UnitTag — exactly
        // Riot's tag-class filter. Tag-based (not `target is Minion`): the C# type Minion also covers jungle
        // monsters (Monster : Minion), wards and pets, which the `Minion` tag correctly excludes (a jungle
        // monster is "Monster | Monster_Large", a ward is "Ward").
        protected override bool IsAcquirableTarget(AttackableUnit target)
        {
            return target.UnitTags.HasTag(UnitTag.Minion);
        }

        // BaronMinionAI.lua OnCollisionEnemy is a no-op (returns without engaging).
        protected override bool EngagesOnCollision => false;
    }
}
