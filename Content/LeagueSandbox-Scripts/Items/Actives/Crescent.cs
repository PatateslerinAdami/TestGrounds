using System;
using System.Numerics;
using GameServerCore.Enums;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ItemTiamatCleave : ItemActiveSpell
    {
        protected override float Cooldown => 10;

        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            if (!owner.CharData.IsMelee)
            {
                return false;
            }

            AddParticleTarget(
                owner,
                owner,
                owner.Inventory.HasItemWithID(3074) ? "tiamatmelee_itm_hydra_active.troy" : "tiamatmelee_itm_active.troy",
                owner,
                1f,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
            foreach (var unit in EnumerateUnitsInRange(owner.Position, 400f, true))
            {
                if (unit.IsDead || unit.Team == owner.Team || unit is not ObjAIBase || unit is BaseTurret
                    || unit is Minion { IsWard: true })
                {
                    continue;
                }

                float distanceRatio = Vector2.Distance(owner.Position, unit.Position) / 400f;
                float multiplier = Math.Clamp(1f - (distanceRatio * 0.4f), 0.6f, 1f);
                float damage = owner.Stats.AttackDamage.Total * multiplier;
                AddParticleTarget(owner, unit, "globalhit_physical.troy", unit, 0.5f, bone: "C_BUFFBONE_GLB_CHEST_LOC");
                var result = unit.TakeDamage(
                    owner,
                    damage,
                    DamageType.DAMAGE_TYPE_PHYSICAL,
                    DamageSource.DAMAGE_SOURCE_PROC,
                    false
                );
                if (owner.Inventory.HasItemWithID(3074))
                {
                    float healAmount = result.PostMitigationDamage * owner.Stats.LifeSteal.Total;
                    owner.TakeHeal(owner, healAmount, HealType.LifeSteal);
                }
            }

            return true;
        }
    }
}
