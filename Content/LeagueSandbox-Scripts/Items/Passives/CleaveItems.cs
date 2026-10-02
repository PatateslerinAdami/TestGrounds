using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3077 : OnHitItem
    {
        protected virtual bool Hydra => false;

        protected override void OnHit(DamageData data)
        {
            if (!Owner.CharData.IsMelee || (!Hydra && Owner.Inventory.HasItemWithID(3074)))
            {
                return;
            }

            AddParticleTarget(
                Owner,
                data.Target,
                Hydra ? "tiamatmelee_itm_hydra.troy" : "tiamatmelee_itm.troy",
                data.Target,
                0.5f,
                bone: "C_BUFFBONE_GLB_GROUND_LOC"
            );
            foreach (var unit in EnumerateUnitsInRange(data.Target.Position, 385, true))
            {
                if (unit == data.Target || unit.IsDead || unit.Team == Owner.Team || unit is not ObjAIBase
                    || unit is BaseTurret
                    || unit is Minion { IsWard: true })
                {
                    continue;
                }

                float multiplier = Math.Clamp(
                    0.6f - Vector2.Distance(unit.Position, data.Target.Position) / 385 * 0.4f,
                    0.2f,
                    0.6f
                );
                var result = unit.TakeDamage(
                    Owner,
                    Owner.Stats.AttackDamage.Total * multiplier,
                    DamageType.DAMAGE_TYPE_PHYSICAL,
                    DamageSource.DAMAGE_SOURCE_PROC,
                    false
                );
                if (Hydra)
                {
                    Owner.TakeHeal(
                        Owner,
                        result.PostMitigationDamage * Owner.Stats.LifeSteal.Total,
                        HealType.LifeSteal
                    );
                }
            }
        }
    }

    public class ItemID_3074 : ItemID_3077
    {
        protected override bool Hydra => true;
    }

    public class ItemID_3085 : OnHitItem
    {
        private bool _bolts;
        public override void OnUpdate(float diff)
        {
            ItemHelpers.ItemEffectHelpers.UpdateTooltip(Owner, 3085, 0, Owner.Stats.AttackDamage.Total * 0.5f);
        }

        protected override void OnHit(DamageData data)
        {
            if (Owner.CharData.IsMelee || _bolts)
            {
                return;
            }

            _bolts = true;
            try
            {
                int count = 0;
                foreach (var unit in System.Linq.Enumerable.OrderBy(EnumerateUnitsInRange(
                    data.Target.Position,
                    375,
                    true
                ), u => Vector2.DistanceSquared(u.Position, data.Target.Position)))
                {
                    if (unit == data.Target || unit.IsDead || unit.Team == Owner.Team || unit is not ObjAIBase
                        || unit is BaseTurret
                        || unit is Minion { IsWard: true }
                        || !unit.GetIsTargetableToTeam(Owner.Team)
                        || Vector2.DistanceSquared(unit.Position, Owner.Position) > (Owner.Stats.Range.Total + 100) * (Owner.Stats.Range.Total + 100))
                    {
                        continue;
                    }

                    Owner.SetSpell("ItemHurricaneAttack", (byte)SpellSlotType.TempItemSlot, true, networked: false);
                    CreateCustomMissile(
                        Owner,
                        0,
                        SpellSlotType.TempItemSlot,
                        Owner.Position,
                        unit.Position,
                        new LeagueSandbox.GameServer.Scripting.CSharp.MissileParameters { Type = MissileType.Target },
                        target: unit
                    );
                    AddParticleTarget(
                        Owner,
                        unit,
                        "globalhit_physical.troy",
                        unit,
                        0.5f,
                        bone: "C_BUFFBONE_GLB_CHEST_LOC"
                    );
                    unit.TakeDamage(
                        Owner,
                        10 + Owner.Stats.AttackDamage.Total * 0.5f,
                        DamageType.DAMAGE_TYPE_PHYSICAL,
                        DamageSource.DAMAGE_SOURCE_ATTACK,
                        false
                    );
                    if (++count == 2)
                    {
                        break;
                    }
                }
            }
            finally
            {
                _bolts = false;
            }
        }
    }
}

namespace Spells
{
    public class ItemHurricaneAttack : GameServerCore.Scripting.CSharp.ISpellScript
    {
        public LeagueSandbox.GameServer.Scripting.CSharp.SpellScriptMetadata ScriptMetadata { get; } = new LeagueSandbox.GameServer.Scripting.CSharp.SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false,
            DoesntBreakShields = true,
            TriggersSpellCasts = false
        };
    }
}
