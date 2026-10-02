using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ItemWraithCollar : ItemActiveSpell
    {
        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            var enemies = EnumerateUnitsInRange(
                owner.Position,
                float.MaxValue,
                true
            ).OfType<Champion>().Where(c => c.Team != owner.Team).OrderBy(c => Vector2.DistanceSquared(c.Position, owner.Position)).Take(2).ToArray();
            for (int i = 0; i < 2; i++)
            {
                float xOffset = (i == 0) ? -30f : 30f;
                Vector2 spawnPos = owner.Position + new Vector2(xOffset, 0f);
                var ghost = AddMinion(
                    owner,
                    "LesserWraith",
                    "Twin Shadows",
                    spawnPos,
                    owner.Team,
                    ignoreCollision: true,
                    targetable: false,
                    useSpells: false
                );
                ghost.SetStatus(GameServerCore.Enums.StatusFlags.Invulnerable, true);
                var state = new BuffVariables();
                state.Set("target", enemies.ElementAtOrDefault(i));
                AddBuff("ItemWraithHunt", 12f, 1, spell, ghost, owner, buffVariables: state);
            }

            return true;
        }
    }
}
