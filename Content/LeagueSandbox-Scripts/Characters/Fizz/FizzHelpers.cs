using System;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    internal static class FizzHelpers
    {
        internal static Vector2 Aim(Spell spell)
        {
            return new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);
        }

        internal static Vector2 Direction(ObjAIBase owner, Vector2 end)
        {
            var dir = end - owner.Position;
            if (dir.LengthSquared() < 0.001f)
            {
                dir = new Vector2(owner.Direction.X, owner.Direction.Z);
            }

            return dir.LengthSquared() > 0.001f ? Vector2.Normalize(dir) : Vector2.UnitX;
        }

        internal static Vector2 Clamp(ObjAIBase owner, Vector2 end, float range)
        {
            return owner.Position + Direction(owner, end) * MathF.Min(range, Vector2.Distance(owner.Position, end));
        }

        internal static bool Eligible(ObjAIBase owner, AttackableUnit target)
        {
            return target != null
                && target != owner
                && target.Team != owner.Team
                && !target.IsDead
                && !target.IsToRemove()
                && target is not ObjBuilding
                && target is not Minion { IsWard: true }
                && target.GetIsTargetableToTeam(owner.Team);
        }

        internal static bool ChampionTarget(AttackableUnit target)
        {
            return target is Champion or Pet { IsClone: true };
        }

        internal static Buffs.FizzPlayful Playful(ObjAIBase owner)
        {
            return owner.GetBuffWithName("FizzPlayful") is { Remove: false, BuffScript: Buffs.FizzPlayful playful } ? playful : null;
        }

        internal static void CancelAttack(ObjAIBase owner)
        {
            if (owner.AutoAttackSpell != null && owner.IsAttacking)
            {
                owner.CancelAutoAttack(false, true);
            }
        }
    }
}
