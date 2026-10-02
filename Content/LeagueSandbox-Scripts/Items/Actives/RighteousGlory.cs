using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ItemRighteousGlory : ItemActiveSpell
    {
        public override void OnActivate(ObjAIBase owner, Spell spell)
        {
            base.OnActivate(owner, spell);
            ApiEventManager.OnSpellPress.AddListener(this, spell, (cast, info) =>
            {
                if (!info.CouldCast && owner.HasBuff("ItemRighteousGlory"))
                {
                    RemoveBuff(owner, "ItemRighteousGlory");
                }
            });
        }

        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            var controller = AddBuff("ItemRighteousGlory", 3, 1, spell, owner, owner);
            var recipients = new List<AttackableUnit>();
            foreach (var ally in EnumerateUnitsInRange(owner.Position, 600, true).OfType<Champion>())
            {
                if (ally.Team != owner.Team)
                {
                    continue;
                }

                AddBuff("ItemRighteousGlorySpeed", 3, 1, spell, ally, owner);
                recipients.Add(ally);
            }

            controller.Variables.Set("recipients", recipients);
            return true;
        }
    }
}
