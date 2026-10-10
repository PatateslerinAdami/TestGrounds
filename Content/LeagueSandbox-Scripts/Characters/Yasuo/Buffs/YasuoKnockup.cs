using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoQ3Mis : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            BuffType = BuffType.KNOCKUP,
            IsHidden = true,
            SendHiddenToClient = true,
        };

        public StatsModifier StatsModifier { get; private set; }

        private ForceMovementParameters _movement;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            buff.SetStatusEffect(StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMove, false);
            float desiredDuration = Math.Max(0.001f, buff.Duration);
            Vector2 startPosition = unit.Position;
            Vector2 direction = new Vector2(unit.Direction.X, unit.Direction.Z);
            if (direction.LengthSquared() < 0.001f)
            {
                direction = Vector2.UnitX;
            }

            direction = Vector2.Normalize(direction);
            var movement = new ForceMovementParameters
            {
                TargetPosition = startPosition + direction * 50,
                ParabolicStartPoint = startPosition,
                Duration = desiredDuration,
                PathSpeedOverride = 50 / desiredDuration,
                ParabolicGravity = 10,
                KeepFacingDirection = true,
                MovementOrdersFacing = ForceMovementOrdersFacing.KEEP_CURRENT_FACING,
                MovementName = "YasuoQ3",
                Caster = ownerSpell.CastInfo.Owner
            };
            unit.StartForcedMovement(movement);
            if (unit.MovementParameters == movement)
            {
                _movement = movement;
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_movement != null && unit.MovementParameters == _movement)
            {
                CancelDash(unit);
            }
        }
    }

    internal class YasuoQ3MisMinion : YasuoQ3Mis
    {
    }
}
