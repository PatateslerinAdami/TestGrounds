using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class FizzMarinerDoomBomb : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnDeath.AddListener(this, unit, _ => RemoveBuff(buff));
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (buff.TimeElapsed < buff.Duration || unit.IsDead || unit.IsToRemove())
            {
                buff.Variables.Get<FizzFish>("fish")?.BombRemoved(buff);
            }

            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class FizzMarinerDoomSlow : FizzWSlow
    {
    }

    public class FizzKnockup : IBuffGameScript
    {
        public virtual BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            SendHiddenToClient = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ForceMovementParameters _movement;

        protected virtual bool Knockback
        {
            get
            {
                return false;
            }
        }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            buff.SetStatusEffect(StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMove, false);
            var center = buff.Variables.Get<Vector2>("center", unit.Position);
            var direction = Knockback ? unit.Position - center : new Vector2(unit.Direction.X, unit.Direction.Z);
            if (direction.LengthSquared() < 0.001f)
            {
                direction = Vector2.UnitX;
            }

            direction = Vector2.Normalize(direction);
            float distance = Knockback ? 250 : 50;
            var movement = new ForceMovementParameters
            {
                MovementName = Knockback ? "FizzMoveback" : "FizzKnockup",
                Caster = buff.SourceUnit,
                TargetPosition = unit.Position + direction * distance,
                ParabolicStartPoint = unit.Position,
                Duration = buff.Duration,
                PathSpeedOverride = distance / MathF.Max(0.001f, buff.Duration),
                ParabolicGravity = Knockback ? 0 : 10,
                KeepFacingDirection = true,
                MovementOrdersFacing = ForceMovementOrdersFacing.KEEP_CURRENT_FACING,
                SetStatus = StatusFlags.None
            };
            unit.StartForcedMovement(movement);
            if (unit.MovementParameters == movement)
            {
                _movement = movement;
            }

            ApiEventManager.OnDeath.AddListener(this, unit, _ => RemoveBuff(buff));
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_movement != null && unit.MovementParameters == _movement)
            {
                CancelDash(unit);
            }

            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class FizzMoveback : FizzKnockup
    {
        public override BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.KNOCKBACK,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            SendHiddenToClient = true
        };

        protected override bool Knockback
        {
            get
            {
                return true;
            }
        }
    }

    public class FizzSharkDissappear : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            IsHidden = true,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();
    }
}
