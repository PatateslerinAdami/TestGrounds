using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoRSequence : IBuffGameScript
    {
        private Spells.YasuoRKnockUpComboW _controller;
        private Buff _buff;

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };

        public StatsModifier StatsModifier
        {
            get
            {
                return null;
            }
        }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _buff = buff;
            _controller = buff.ParentScript as Spells.YasuoRKnockUpComboW;
            buff.SetStatusEffect(StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast, false);
        }

        public void OnUpdate(float diff)
        {
            _controller?.AdvanceSequence(diff);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _controller?.OnSequenceRemoved(_buff);
        }
    }

    internal class YasuoRStun : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier
        {
            get
            {
                return null;
            }
        }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            buff.SetStatusEffect(StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast, false);
        }
    }

    internal class YasuoRKnockUpCombo : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            SendHiddenToClient = true
        };

        public StatsModifier StatsModifier
        {
            get
            {
                return null;
            }
        }
    }

    internal class YasuoRKnockUpComboTar : IBuffGameScript
    {
        private AttackableUnit _unit;
        private ForceMovementParameters _movement;
        private Spells.YasuoRKnockUpComboW _controller;
        private Spell _spell;
        private float _pulseTime;
        private Buff _buff;
        private Region _reveal;

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true,
            SendHiddenToClient = true
        };

        public StatsModifier StatsModifier
        {
            get
            {
                return null;
            }
        }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _unit = unit;
            _buff = buff;
            _spell = spell;
            _controller = buff.ParentScript as Spells.YasuoRKnockUpComboW;
            buff.SetStatusEffect(StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast, false);
            buff.SetStatusEffect(StatusFlags.RevealSpecificUnit, true);
            _reveal = AddUnitPerceptionBubble(unit, 1, buff.Duration, spell.CastInfo.Owner.Team, true, unit, ignoresLoS: true,
                onlyShowTarget: true);
            FaceDirection(spell.CastInfo.Owner.Position, unit, true);
            unit.StopMovement();
            NotifyWaypointGroup(unit);
        }

        private void Pulse(int pulseCount)
        {
            var direction = new Vector2(_unit.Direction.X, _unit.Direction.Z);
            direction = direction.LengthSquared() > 0.001f ? Vector2.Normalize(direction) : Vector2.UnitX;
            direction = Vector2.Transform(direction, Matrix3x2.CreateRotation(-2 * MathF.PI / 3 * (pulseCount % 3)));
            _unit.FaceDirection(new Vector3(direction.X, 0, direction.Y));
            var movement = new ForceMovementParameters
            {
                TargetPosition = _unit.Position + direction * 10,
                ParabolicStartPoint = _unit.Position,
                PathSpeedOverride = 10,
                ParabolicGravity = 5,
                MovementName = "YasuoR",
                Caster = _spell.CastInfo.Owner,
                MovementOrdersType = ForceMovementOrdersType.CANCEL_ORDER,
                MovementOrdersFacing = ForceMovementOrdersFacing.KEEP_CURRENT_FACING,
                KeepFacingDirection = true
            };
            _unit.StartForcedMovement(movement);
            if (_unit.MovementParameters == movement)
            {
                _movement = movement;
            }
        }

        public void OnUpdate(float diff)
        {
            if (_unit.IsDead || _controller?.IsExecuting != true)
            {
                return;
            }

            _pulseTime += diff / 1000f;
            if (_pulseTime < 0.25f)
            {
                return;
            }

            int pulseCount = (int)(_pulseTime / 0.25f);
            _pulseTime -= pulseCount * 0.25f;
            if (_unit.MovementParameters != null && _unit.MovementParameters != _movement)
            {
                return;
            }

            Pulse(pulseCount);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            if (_movement != null && unit.MovementParameters == _movement)
            {
                CancelDash(unit);
            }

            _reveal?.SetToRemove();
            _controller?.OnTargetHoldRemoved(_buff);
        }
    }

    internal class YasuoRArmorPen : IBuffGameScript
    {
        private Particle _swordGlow;

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            StatsModifier.ArmorPenetration.PercentBonus = spell.SpellData.GetEffectAmount(5, spell.CastInfo.SpellLevel) / 100f;
            unit.AddStatModifier(StatsModifier);
            _swordGlow = AddParticleTarget(buff.SourceUnit, unit, "Yasuo_Base_R_SwordGlow.troy", unit, lifetime: buff.Duration,
                boneNameHash: 185113601, targetBoneNameHash: 185113601);
            ApiEventManager.OnDeath.AddListener(this, unit, _ => RemoveBuff(buff));
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_swordGlow);
            _swordGlow = null;
        }
    }
}
