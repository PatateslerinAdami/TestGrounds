using System.Numerics;
using GameServerCore.Enums;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ItemPromote : ItemActiveSpell
    {
        protected override float Cooldown => 120;

        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            bool isInvalidTarget = target is not LaneMinion minion
                || minion.MinionSpawnType == MinionSpawnType.MINION_TYPE_SUPER
                || target.IsDead
                || target.Team != owner.Team
                || target.HasBuff("SummonerPromoteSR")
                || Vector2.DistanceSquared(owner.Position, target.Position) > 1000f * 1000f;
            if (isInvalidTarget)
            {
                return false;
            }

            AddBuff("SummonerPromoteSR", 0, 1, spell, target, owner, true);
            return true;
        }
    }
}
