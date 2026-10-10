using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class FizzPiercingStrike : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            CastingBreaksStealth = true
        };

        private ObjAIBase _owner;
        private Spell _spell;
        private AttackableUnit _target;
        private ForceMovementParameters _movement;
        private Particle _trail;
        private Vector2 _lastPosition;
        private float _attackDamage, _magicDamage;
        private bool _hit;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
            ApiEventManager.OnMoveSuccess.AddListener(this, owner, OnSuccess);
            ApiEventManager.OnMoveFailure.AddListener(this, owner, OnFailure);
            ApiEventManager.OnDeath.AddListener(this, owner, _ => Cleanup());
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (!FizzHelpers.Eligible(owner, target) || _movement != null)
            {
                spell.CancelCast();
                return;
            }

            _target = target;
            _attackDamage = owner.Stats.AttackDamage.Total;
            _magicDamage = spell.SpellData.GetEffectAmount(1, spell.CastInfo.SpellLevel) + spell.SpellData.Coefficient * owner.Stats.AbilityPower.Total;
            _hit = false;
        }

        public void OnSpellPostCast(Spell spell)
        {
            var direction = FizzHelpers.Direction(_owner, _target.Position);
            FizzHelpers.CancelAttack(_owner);
            _lastPosition = _owner.Position;
            _movement = new ForceMovementParameters
            {
                MovementName = "FizzPiercingStrike",
                Caster = _owner,
                TargetPosition = _owner.Position + direction * 550,
                ParabolicStartPoint = _owner.Position,
                PathSpeedOverride = 1400,
                Animation = "Spell1",
                AnimationFlags = (AnimationFlags)132,
                AnimationTimeScale = 0,
                AnimationSpeedScale = 1,
                DoStopAnimation = true,
                StopAnimationFlags = StopAnimationFlags.Fade | StopAnimationFlags.IgnoreLock,
                SetStatus = StatusFlags.CanAttack,
                MovementOrdersType = ForceMovementOrdersType.CANCEL_ORDER,
                BufferSpellCasts = true
            };
            _trail = AddParticleTarget(_owner, _owner, "Fizz_PiercingStrike.troy", _owner, lifetime: 1, bone: "root",
                direction: new Vector3(direction.X, 0, direction.Y));
            var movement = _movement;
            _owner.StartForcedMovement(movement);
            if (_owner.MovementParameters != movement)
            {
                Cleanup();
                return;
            }

            CheckContact();
        }

        private void CheckContact()
        {
            if (_hit || !FizzHelpers.Eligible(_owner, _target))
            {
                return;
            }

            var segment = _owner.Position - _lastPosition;
            float t = segment.LengthSquared() > 0.001f ? Math.Clamp(Vector2.Dot(_target.Position - _lastPosition, segment) / segment.LengthSquared(), 0, 1) : 0;
            var closest = _lastPosition + segment * t;
            if (Vector2.DistanceSquared(closest, _target.Position) > 150 * 150)
            {
                return;
            }

            _hit = true;
            var target = _target;
            target.TakeDamage(_owner, _magicDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, _spell);
            if (target.IsDead || target.IsToRemove() || _owner.IsDead)
            {
                return;
            }

            var hit = new DamageData
            {
                Attacker = _owner,
                Target = target,
                Damage = _attackDamage,
                PostMitigationDamage = target.Stats.GetPostMitigationDamage(_attackDamage, DamageType.DAMAGE_TYPE_PHYSICAL, _owner),
                DamageType = DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource = DamageSource.DAMAGE_SOURCE_ATTACK,
                IsAutoAttack = false,
                AppliesOnHitEffects = true,
                UsesLifeSteal = true
            };
            target.TakeDamage(hit, DamageResultType.RESULT_NORMAL, _spell);
            AddParticleTarget(_owner, target, "Fizz_PiercingStrike_tar.troy", target, lifetime: 0.5f);
        }

        public void OnUpdate(float diff)
        {
            if (_movement == null)
            {
                return;
            }

            CheckContact();
            _lastPosition = _owner.Position;
        }

        private void OnSuccess(AttackableUnit unit, ForceMovementParameters movement)
        {
            if (movement != _movement)
            {
                return;
            }

            CheckContact();
            _movement = null;
            RemoveParticle(_trail);
            _trail = null;
        }

        private void OnFailure(AttackableUnit unit, ForceMovementParameters movement)
        {
            if (movement != _movement)
            {
                return;
            }

            _movement = null;
            RemoveParticle(_trail);
            _trail = null;
        }

        private void Cleanup()
        {
            var movement = _movement;
            _movement = null;
            if (movement != null && _owner.MovementParameters == movement)
            {
                CancelDash(_owner);
            }

            RemoveParticle(_trail);
            _trail = null;
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            Cleanup();
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class FizzQ1 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true
        };
    }
}
