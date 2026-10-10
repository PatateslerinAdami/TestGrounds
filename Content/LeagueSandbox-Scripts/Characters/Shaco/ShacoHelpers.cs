using System;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ShacoScripts
{
    internal static class ShacoHelpers
    {
        public static bool IsBehind(AttackableUnit attacker, AttackableUnit target)
        {
            var facing = new Vector2(target.Direction.X, target.Direction.Z);
            var towardAttacker = attacker.Position - target.Position;
            if (facing.LengthSquared() < 0.0001f || towardAttacker.LengthSquared() < 0.0001f)
            {
                return false;
            }

            return Vector2.Dot(Vector2.Normalize(facing), Vector2.Normalize(towardAttacker)) < -0.0001f;
        }

        public static bool CanAttack(ObjAIBase owner, AttackableUnit target)
        {
            return target != null && !target.IsDead && !target.IsToRemove()
                && target.Team != owner.Team && target.GetIsTargetableToTeam(owner.Team)
                && target.Status.HasFlag(StatusFlags.Targetable) && TeamHasVision(owner.Team, target);
        }

        public static Stats CloneStats(ObjAIBase owner)
        {
            var stats = owner.Stats.CreateSnapshot();

            foreach (var buff in owner.GetBuffs())
            {
                if (buff.Remove) continue;
                var modifier = buff.GetStatsModifier();
                if (modifier != null)
                {
                    stats.RemoveModifier(modifier);
                }
            }

            foreach (var modifier in owner.Inventory.GetPassiveStatModifiers())
            {
                stats.RemoveModifier(modifier);
            }

            foreach (var item in owner.Inventory.GetAllItems())
            {
                for (int stack = 0; stack < item.StackCount; stack++)
                {
                    stats.RemoveModifier(item.ItemData);
                }
            }

            stats.ExpGivenOnDeath.BaseValue = 0;
            stats.ExpGivenOnDeath.BaseBonus = 0;
            stats.GoldGivenOnDeath.BaseValue = 25;
            stats.GoldGivenOnDeath.BaseBonus = 0;
            stats.CalculateTrueMoveSpeed();
            return stats;
        }
    }
}
