using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class FioraQ : ISpellScript, IEventSource
    {
        private ObjAIBase _owner;
        private Spell _spell;
        private AttackableUnit _target;
        private Buff _recast, _dashBuff;
        private ForceMovementParameters _movement;
        private bool _isRecast;
        private float _damage;
        private byte _rank;
        private uint _version;

        public uint ScriptNameHash
        {
            get
            {
                return _spell?.ScriptNameHash ?? 0;
            }
        }

        public IEventSource ParentScript
        {
            get
            {
                return _spell;
            }
        }

        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            CastingBreaksStealth = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
            ApiEventManager.OnDeath.AddListener(this, owner, _ => Cleanup());
        }

        private bool Eligible(AttackableUnit unit)
        {
            return unit != null
                && !unit.IsDead
                && !unit.IsToRemove()
                && unit.Status.HasFlag(StatusFlags.Targetable)
                && unit.GetIsTargetableToTeam(_owner.Team)
                && IsValidTarget(_owner, unit, _spell.SpellData.Flags);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (!Eligible(target) || _movement != null || spell.CastInfo.SpellLevel < 1)
            {
                spell.CancelCast();
                return;
            }

            _target = target;
            _rank = spell.CastInfo.SpellLevel;
            _damage = spell.SpellData.GetEffectAmount(1, _rank) + spell.SpellData.Coefficient * (owner.Stats.AttackDamage.Total - owner.Stats.AttackDamage.BaseValue);
            _isRecast = _recast != null && !_recast.Remove;
        }

        public void OnSpellPostCast(Spell spell)
        {
            uint version = unchecked(++_version);
            if (_isRecast)
            {
                FinishRecast();
            }
            else
            {
                _recast = AddBuff("FioraQCD", spell.SpellData.GetEffectAmount(2, _rank), 1, spell, _owner, _owner, parent: this);
                if (_recast != null && !_recast.Remove && _owner.HasBuff(_recast))
                {
                    spell.SetManaCostOverride(0);
                    spell.SetCooldown(0.1f, true);
                }
                else
                {
                    _recast = null;
                    spell.SetCooldown(spell.GetCooldown(), true);
                }
            }

            if (!Eligible(_target))
            {
                return;
            }

            if (_owner.IsDead)
            {
                return;
            }

            if (_owner.IsToRemove())
            {
                return;
            }

            if (version != _version)
            {
                return;
            }

            var target = _target;
            float damage = _damage;
            byte rank = _rank;
            var lunge = CreateSpell(_owner, "FioraQLunge", 45);
            lunge.CastInfo.SpellLevel = rank;
            var direction = target.Position - _owner.Position;
            float distance = direction.Length();
            direction = distance > 0.001f ? direction / distance : Vector2.UnitX;
            var landing = distance > 100 ? target.Position - direction * 100 : _owner.Position;
            var movement = new ForceMovementParameters
            {
                MovementName = "FioraQ",
                Caster = _owner,
                TargetPosition = landing,
                ParabolicStartPoint = _owner.Position,
                PathSpeedOverride = MathF.Max(1550, Vector2.Distance(_owner.Position, landing) / 0.25f),
                ParabolicGravity = 0,
                Animation = "Spell1",
                AnimationTimeScale = 0.4f,
                AnimationSpeedScale = 1,
                AnimationFlags = (AnimationFlags)192,
                SetStatus = StatusFlags.CanAttack | StatusFlags.CanMove | StatusFlags.CanCast,
                BlockedSpellSlots = new HashSet<byte> { 0, 1, 2, 3 },
                BufferSpellCasts = true,
                MovementOrdersType = ForceMovementOrdersType.CANCEL_ORDER
            };
            _movement = movement;
            _dashBuff = AddBuff("FioraQDash", 1, 1, spell, _owner, _owner, parent: this);
            Action<AttackableUnit, ForceMovementParameters> success = null;
            Action<AttackableUnit, ForceMovementParameters> failure = null;
            void RemoveListeners()
            {
                ApiEventManager.OnMoveSuccess.RemoveListener(this, _owner, success);
                ApiEventManager.OnMoveFailure.RemoveListener(this, _owner, failure);
            }

            success = (_, completed) =>
            {
                if (completed != movement)
                {
                    return;
                }

                RemoveListeners();
                _movement = null;
                RemoveDashBuff();
                if (version != _version)
                {
                    return;
                }

                if (_owner.IsDead)
                {
                    return;
                }

                if (_owner.IsToRemove())
                {
                    return;
                }

                if (!Eligible(target))
                {
                    return;
                }

                lunge.ApplyEffects(target);
                if (version != _version)
                {
                    return;
                }

                if (_owner.IsDead)
                {
                    return;
                }

                if (_owner.IsToRemove())
                {
                    return;
                }

                if (!Eligible(target))
                {
                    return;
                }

                target.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, lunge);
                if (version != _version || _owner.IsDead || _owner.IsToRemove())
                {
                    return;
                }

                FioraFlurry.ApplyLungeHit(spell, target);
                AddParticleTarget(_owner, target, "FioraQLunge_tar.troy", target, lifetime: 0.5f);
            };
            failure = (_, ended) =>
            {
                if (ended != movement)
                {
                    return;
                }

                RemoveListeners();
                _movement = null;
                RemoveDashBuff();
            };
            ApiEventManager.OnMoveSuccess.AddListener(this, _owner, success);
            ApiEventManager.OnMoveFailure.AddListener(this, _owner, failure);
            _owner.StartForcedMovement(movement);
            if (_owner.MovementParameters != movement)
            {
                RemoveListeners();
                _movement = null;
                RemoveDashBuff();
            }
        }

        internal void OnRecastRemoved(Buff buff)
        {
            if (_recast == buff)
            {
                FinishRecast();
            }
        }

        private void FinishRecast()
        {
            var buff = _recast;
            _recast = null;
            _spell.SetManaCostOverride(null);
            _spell.SetCooldown(_spell.GetCooldown(), true);
            if (buff != null && !buff.Remove)
            {
                RemoveBuff(buff);
            }
        }

        private void RemoveDashBuff()
        {
            var buff = _dashBuff;
            _dashBuff = null;
            if (buff != null && !buff.Remove)
            {
                RemoveBuff(buff);
            }
        }

        internal void OnDashBuffRemoved(Buff buff)
        {
            if (_dashBuff != buff)
            {
                return;
            }

            _dashBuff = null;
            if (_movement != null && _owner.MovementParameters == _movement)
            {
                CancelDash(_owner);
            }
        }

        private void Cleanup()
        {
            unchecked
            {
                _version++;
            }

            if (_movement != null && _owner.MovementParameters == _movement)
            {
                CancelDash(_owner);
            }

            _movement = null;
            RemoveDashBuff();
            if (_recast != null)
            {
                FinishRecast();
            }
            else
            {
                _spell.SetManaCostOverride(null);
            }

            ApiEventManager.OnMoveSuccess.RemoveListener(this);
            ApiEventManager.OnMoveFailure.RemoveListener(this);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            Cleanup();
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class FioraQLunge : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            IsDeathRecapSource = true
        };
    }
}
