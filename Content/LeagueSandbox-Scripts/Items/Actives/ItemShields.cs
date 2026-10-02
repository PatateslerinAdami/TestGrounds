using System.Numerics;
using Buffs;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ItemSeraphsEmbrace : ItemActiveSpell
    {
        protected override float Cooldown => 120;

        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            var mana = owner.Stats.CurrentMana * 0.2f;
            owner.Stats.CurrentMana -= mana;
            var variables = new BuffVariables();
            variables.Set("shield", 150f + mana);
            AddParticleTarget(
                owner,
                owner,
                "spectral_fury_cas.troy",
                owner,
                1f,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
            AddBuff("ItemSeraphsEmbrace", 3f, 1, spell, owner, owner, buffVariables: variables);
            return true;
        }
    }

    public class IronStylus : ItemActiveSpell
    {
        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            float amount = 50f + (10f * owner.Stats.Level);
            AddParticleTarget(
                owner,
                owner,
                "ironstylus_cas.troy",
                owner,
                1.0f,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
            foreach (var ally in EnumerateUnitsInRange(owner.Position, 600f, true))
            {
                if (ally is not Champion || ally.Team != owner.Team)
                {
                    continue;
                }

                if (ally.GetBuffWithName("IronStylusBuff")?.BuffScript is TimedItemShield old && old.Remaining > amount)
                {
                    continue;
                }

                var variables = new BuffVariables();
                variables.Set("shield", amount);
                AddBuff("IronStylusBuff", 5f, 1, spell, ally, owner, buffVariables: variables);
            }

            return true;
        }
    }
}
