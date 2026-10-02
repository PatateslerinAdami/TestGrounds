using System.Numerics;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class HextechSweeper : ItemActiveSpell
    {
        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            float radius = spell.SpellData.CastRadius[0];
            if (radius <= 0)
            {
                radius = 520;
            }

            AddPosPerceptionBubble(position, radius, 5, owner.Team);
            var variables = new BuffVariables();
            variables.Set("position", position);
            variables.Set("radius", radius);
            AddBuff("HextechSweeperArea", 5, 1, spell, owner, owner, buffVariables: variables);
            return true;
        }
    }
}
