using System.Numerics;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class HealthBomb : ItemActiveSpell
    {
        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            bool isInvalidTarget = target is not Champion || target.IsDead || target.Team != owner.Team
                || Vector2.DistanceSquared(target.Position, owner.Position) > 750f * 750f;
            if (isInvalidTarget)
            {
                return false;
            }

            var variables = new BuffVariables();
            variables.Set("shield", owner.Stats.HealthPoints.Total * 0.1f);
            AddBuff("HealthBomb", 4f, 1, spell, target, owner, buffVariables: variables);
            return true;
        }
    }
}
