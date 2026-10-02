using System.Numerics;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ItemSoTD : ItemActiveSpell
    {
        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            AddBuff("ItemSoTD", 3, 1, spell, owner, owner);
            return true;
        }
    }
}
