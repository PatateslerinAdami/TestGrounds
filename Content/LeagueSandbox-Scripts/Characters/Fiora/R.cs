using System;
using System.Collections.Generic;
using System.Linq;
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
    public class FioraDance : ISpellScript, IEventSource
    {
        private const float StrikeTime = 0.3f;
        private const SpellDataFlags TargetFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes;
        private readonly HashSet<AttackableUnit> _hitTargets = new HashSet<AttackableUnit>();
        private readonly Random _random = new Random();
        private readonly List<Particle> _hitEffects = new List<Particle>();
        private ObjAIBase _owner;
        private Spell _spell;
        private AttackableUnit _original, _pending;
        private Vector2 _center;
        private Buff _sequence, _ghosted;
        private byte _rank;
        private float _damage, _repeatMultiplier, _elapsed;
        private int _strike;
        private uint _version;
        private string _animation;

        public bool IsExecuting
        {
            get
            {
                return _sequence != null && !_sequence.Remove;
            }
        }

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
            ApiEventManager.OnDeath.AddListener(this, owner, _ => CancelSequence());
        }

        private bool Eligible(AttackableUnit unit)
        {
            return unit != null
                && !unit.IsDead
                && !unit.IsToRemove()
                && unit.IsVisibleByTeam(_owner.Team)
                && unit.Status.HasFlag(StatusFlags.Targetable)
                && unit.GetIsTargetableToTeam(_owner.Team)
                && IsValidTarget(_owner, unit, TargetFlags);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (IsExecuting || !Eligible(target) || spell.CastInfo.SpellLevel < 1)
            {
                spell.CancelCast();
                return;
            }

            _original = target;
            _rank = spell.CastInfo.SpellLevel;
            _damage = spell.SpellData.GetEffectAmount(2, _rank) + spell.SpellData.Coefficient * (owner.Stats.AttackDamage.Total - owner.Stats.AttackDamage.BaseValue);
            _repeatMultiplier = spell.SpellData.GetEffectAmount(3, _rank) / 100;
        }

        public void OnSpellPostCast(Spell spell)
        {
            if (!Eligible(_original) || _owner.IsDead || _owner.IsToRemove())
            {
                return;
            }

            _hitTargets.Clear();
            _strike = 0;
            _elapsed = 0;
            _center = _original.Position;
            var version = unchecked(++_version);
            _sequence = AddBuff("FioraDance", 5 * StrikeTime, 1, spell, _owner, _owner, infiniteduration: true, parent: this);
            if (!IsExecuting || !_owner.HasBuff(_sequence) || version != _version)
            {
                CancelSequence();
                return;
            }

            _ghosted = AddBuff("FioraDanceGhosted", 5 * StrikeTime, 1, spell, _owner, _owner);
            StartStrike();
        }

        private AttackableUnit SelectTarget()
        {
            if ((_strike == 0 || _strike == 4) && Eligible(_original))
            {
                return _original;
            }

            var targets = EnumerateValidUnitsInRange(_owner, _center, _spell.SpellData.BounceRadius, true, TargetFlags).Where(Eligible).OrderBy(unit => unit.NetId).ToList();
            return targets.Count == 0 ? null : targets[_random.Next(targets.Count)];
        }

        private void StartStrike()
        {
            if (_strike == 4)
            {
                if (_ghosted != null && !_ghosted.Remove)
                {
                    RemoveBuff(_ghosted);
                }

                _ghosted = null;
            }

            _pending = SelectTarget();
            if (_pending == null)
            {
                CancelSequence();
                return;
            }

            _center = _pending.Position;
            var direction = StrikeDirection(_pending);
            FaceDirection(_pending.Position, _owner, true);
            var carrier = CreateSpell(_owner, "FioraDanceStrike", 46);
            carrier.CastInfo.SpellLevel = _rank;
            _animation = "Spell4" + (char)('a' + _strike);
            carrier.Cast(_owner.Position, _owner.Position, _owner);
            if (!IsExecuting)
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

            if (!Eligible(_pending))
            {
                return;
            }

            if (_strike != 0)
            {
                PlayAnimation(_owner, _animation, scaleTime: 0, speedRatio: 1, flags: (AnimationFlags)228);
            }

            PositionForStrike(_pending, direction);
        }

        private Vector2 StrikeDirection(AttackableUnit target)
        {
            var direction = target.Position - _owner.Position;
            if (direction.LengthSquared() < 0.001f)
            {
                direction = new Vector2(_owner.Direction.X, _owner.Direction.Z);
            }

            return direction.LengthSquared() < 0.001f ? Vector2.UnitX : Vector2.Normalize(direction);
        }

        private void PositionForStrike(AttackableUnit target, Vector2 direction)
        {
            var landing = target.Position + direction * (_strike == 4 ? 95 : 170);
            TeleportTo(_owner, landing.X, landing.Y);
            FaceDirection(target.Position, _owner, true);
        }

        internal void AdvanceSequence(float diff)
        {
            if (!IsExecuting)
            {
                return;
            }

            if (_owner.IsDead || _owner.IsToRemove())
            {
                CancelSequence();
                return;
            }

            _elapsed += diff / 1000;
            if (IsExecuting && _elapsed + 0.00001f >= StrikeTime)
            {
                _elapsed = 0;
                uint version = _version;
                var target = _pending;
                if (!Eligible(target))
                {
                    target = SelectTarget();
                    if (target != null)
                    {
                        PositionForStrike(target, StrikeDirection(target));
                    }
                }

                if (target == null)
                {
                    CancelSequence();
                    return;
                }

                _center = target.Position;
                bool final = _strike == 4;
                if (!final)
                {
                    _strike++;
                    StartStrike();
                    if (!IsExecuting || version != _version)
                    {
                        return;
                    }
                }

                ResolveStrike(target);
                if (!IsExecuting || version != _version)
                {
                    return;
                }

                if (final)
                {
                    FinishSequence();
                }
            }
        }

        private void ResolveStrike(AttackableUnit target)
        {
            if (!Eligible(target))
            {
                return;
            }

            float damage = _damage * (_hitTargets.Add(target) ? 1 : _repeatMultiplier);
            var data = new DamageData
            {
                Attacker = _owner,
                Target = target,
                Damage = damage,
                PostMitigationDamage = target.Stats.GetPostMitigationDamage(damage, DamageType.DAMAGE_TYPE_PHYSICAL, _owner),
                DamageType = DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource = DamageSource.DAMAGE_SOURCE_SPELL,
                IsAutoAttack = false,
                AppliesOnHitEffects = true,
                UsesLifeSteal = true
            };
            _spell.ApplyEffects(target);
            if (!IsExecuting)
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

            target.TakeDamage(data, DamageResultType.RESULT_NORMAL, _spell);
            if (!IsExecuting || _owner.IsDead || _owner.IsToRemove())
            {
                return;
            }

            _hitEffects.Add(AddParticleTarget(_owner, target, "Fiora_Dance_tar.troy", target, lifetime: 0.5f, boneNameHash: 49953683));
        }

        internal void OnSequenceRemoved(Buff buff)
        {
            if (_sequence == buff)
            {
                CancelSequence();
            }
        }

        private void FinishSequence()
        {
            var buff = _sequence;
            _sequence = null;
            if (buff != null && !buff.Remove)
            {
                RemoveBuff(buff);
            }

            if (_ghosted != null && !_ghosted.Remove)
            {
                RemoveBuff(_ghosted);
            }

            _ghosted = null;
            _pending = null;
            _original = null;
            _animation = null;
            _hitTargets.Clear();
            _hitEffects.Clear();
        }

        public void CancelSequence()
        {
            unchecked
            {
                _version++;
            }

            var buff = _sequence;
            _sequence = null;
            if (buff != null && !buff.Remove)
            {
                RemoveBuff(buff);
            }

            if (_ghosted != null && !_ghosted.Remove)
            {
                RemoveBuff(_ghosted);
            }

            _ghosted = null;
            if (_animation != null)
            {
                StopAnimation(_owner, _animation, fade: true);
            }

            _animation = null;
            foreach (var effect in _hitEffects)
            {
                RemoveParticle(effect);
            }

            _hitEffects.Clear();
            _hitTargets.Clear();
            _pending = null;
            _original = null;
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            CancelSequence();
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class FioraDanceStrike : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata();
    }
}
