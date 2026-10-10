using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class AatroxQ : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType= BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            SendHiddenToClient = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();
    }

    public class AatroxQDescent : AatroxQ
    {
    }

    public class AatroxQKnockup : IBuffGameScript
    {
        private ForceMovementParameters _movement;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            BuffMetaData.ClientPackageHash = spell.CastInfo.Owner.GetObjHash();
            buff.SetStatusEffect(StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMove, false);
            var direction = new Vector2(unit.Direction.X, unit.Direction.Z);
            if (direction.LengthSquared() < 0.001f)
            {
                direction = Vector2.UnitX;
            }

            var movement = new ForceMovementParameters
            {
                TargetPosition = unit.Position + Vector2.Normalize(direction) * 8.25f,
                ParabolicStartPoint = unit.Position,
                Duration = buff.Duration,
                PathSpeedOverride = 8.25f / buff.Duration,
                ParabolicGravity = 20,
                KeepFacingDirection = true,
                MovementOrdersFacing = ForceMovementOrdersFacing.KEEP_CURRENT_FACING,
                Caster = spell.CastInfo.Owner,
                MovementName = "AatroxQKnockup"
            };
            unit.StartForcedMovement(movement);
            if (unit.MovementParameters == movement)
            {
                _movement = movement;
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            if (_movement != null && unit.MovementParameters == _movement)
            {
                CancelDash(unit);
            }
        }
    }
}
