using System.Numerics;
using GameServerCore.Enums;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class QuicksilverSash : ItemActiveSpell
    {
        protected override float Cooldown => 90;

        protected virtual string CleanseParticle => "summoner_cast.troy";

        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            ItemCleanseHelpers.Cleanse(owner, true);
            AddParticleTarget(
                owner,
                owner,
                CleanseParticle,
                owner,
                1f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                ignoreCasterVisibility: true
            );
            return true;
        }
    }

    public class ItemDervishBlade : QuicksilverSash
    {
        protected override string CleanseParticle => "summoner_boost.troy";

        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            base.Use(owner, spell, target, position);
            if (owner.CharData.IsMelee)
            {
                var variables = new BuffVariables();
                variables.Set("speed", 0.5f);
                AddBuff("ItemDervishBlade", 1f, 1, spell, owner, owner, buffVariables: variables);
            }

            return true;
        }
    }

    public class ItemMercurial : QuicksilverSash
    {
        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            base.Use(owner, spell, target, position);
            AddParticleTarget(
                owner,
                owner,
                "summoner_boost.troy",
                owner,
                1f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                ignoreCasterVisibility: true
            );
            var variables = new BuffVariables();
            variables.Set("speed", 0.5f);
            AddBuff("ItemMercurial", 1f, 1, spell, owner, owner, buffVariables: variables);
            return true;
        }
    }

    public class ItemMorellosBane : ItemActiveSpell
    {
        protected override float Cooldown => 180;

        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            bool isInvalidTarget = target is not Champion || target.IsDead || target.Team != owner.Team
                || Vector2.DistanceSquared(owner.Position, target.Position) > 750f * 750f;
            if (isInvalidTarget)
            {
                return false;
            }

            ItemCleanseHelpers.Cleanse(target, false);
            float healAmount = 150f + (target.Stats.HealthPoints.Total * 0.1f);
            target.TakeHeal(owner, healAmount, HealType.OutgoingHeal);
            AddParticleTarget(
                owner,
                target,
                "summoner_boost.troy",
                target,
                1.0f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                ignoreCasterVisibility: true
            );
            return true;
        }
    }

    public class RanduinsOmen : ItemActiveSpell
    {
        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            float totalResists = owner.Stats.Armor.Total + owner.Stats.MagicResist.Total;
            float duration = 2f + (totalResists / 200f);
            AddParticleTarget(
                owner,
                owner,
                "randuinsomen_cas.troy",
                owner,
                1.0f,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
            foreach (var enemy in EnumerateUnitsInRange(owner.Position, 500f, true))
            {
                if (enemy.Team == owner.Team || enemy is not ObjAIBase || enemy is BaseTurret
                    || enemy is Minion { IsWard: true })
                {
                    continue;
                }

                var variables = new BuffVariables();
                variables.Set("slow", 0.35f);
                AddBuff("CrippleSlow", duration, 1, spell, enemy, owner, buffVariables: variables);
            }

            return true;
        }
    }
}
