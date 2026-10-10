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
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class YasuoRKnockUpComboW : ISpellScript, IEventSource
    {
        internal const float SuspensionTime = 1f;
        private const float TargetRadius = 400f;
        private const float BlinkOffset = 120f;
        private const SpellDataFlags TargetFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes;
        private readonly List<HeldTarget> _held = new List<HeldTarget>();
        private readonly List<Particle> _particles = new List<Particle>();
        private readonly Dictionary<AttackableUnit, Particle> _indicators = new Dictionary<AttackableUnit, Particle>();
        private ObjAIBase _owner;
        private Spell _spell, _dummy;
        private SpellMissile _rMissile;
        private AttackableUnit _selected;
        private List<AttackableUnit> _selectedTargets;
        private Buff _sequenceBuff;
        private Minion _clone;
        private GameScriptTimer _cloneTimer, _cloneHideTimer;
        private Particle _cloneEffect;
        private bool _cloneHidden;
        private float _elapsed, _damage;
        private Vector2 _center;
        private uint _sequenceVersion;

        public bool IsExecuting
        {
            get
            {
                return _sequenceBuff != null && !_sequenceBuff.Remove;
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
            NotSingleTargetSpell = true,
            CastingBreaksStealth = true
        };

        private sealed class HeldTarget
        {
            internal AttackableUnit Unit;
            internal Buff Combo, Stun, Hold;
            internal bool Resolved;
        }

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
            spell.SetIconIndex(1);
            ApiEventManager.OnDeath.AddListener(this, owner, OnDeath);
        }

        private bool Eligible(AttackableUnit unit, bool requireRange)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.IsDead)
            {
                return false;
            }

            if (unit.IsToRemove())
            {
                return false;
            }

            if (!IsValidTarget(_owner, unit, TargetFlags))
            {
                return false;
            }

            if ((requireRange && !unit.IsVisibleByTeam(_owner.Team)))
            {
                return false;
            }

            if (!unit.GetIsTargetableToTeam(_owner.Team))
            {
                return false;
            }

            if (unit.Status.HasFlag(StatusFlags.Immovable))
            {
                return false;
            }

            if (unit.IsUnstoppable)
            {
                return false;
            }

            if (unit.GetBuffs().Any(buff => !buff.Remove && buff.BuffType == BuffType.SPELL_IMMUNITY))
            {
                return false;
            }

            if (requireRange
                && Vector2.DistanceSquared(_owner.Position, unit.Position) > _spell.GetCurrentCastRange() * _spell.GetCurrentCastRange())
            {
                return false;
            }

            return unit.GetBuffs().Any(buff => !buff.Remove
                && (buff.BuffType == BuffType.KNOCKUP
                || buff.BuffType == BuffType.KNOCKBACK)
                && (buff.IsBuffInfinite()
                || buff.TimeElapsed < buff.Duration)
                && (buff.SourceUnit == null
                || buff.SourceUnit.Team != unit.Team));
        }

        private IEnumerable<AttackableUnit> EligibleInRange(Vector2 position, float range, bool requireRange)
        {
            return EnumerateValidUnitsInRange(_owner, position, range, true, TargetFlags).Where(unit => Eligible(unit, requireRange));
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _selected = null;
            _selectedTargets = null;
            if (IsExecuting || owner.IsDead || spell.CastInfo.SpellLevel < 1)
            {
                spell.CancelCast();
                return;
            }

            if (target != null && target != owner)
            {
                if (Eligible(target, true))
                {
                    _selected = target;
                }
            }
            else
            {
                _selected = EligibleInRange(owner.Position, spell.GetCurrentCastRange(), true).OrderBy(unit => Vector2.DistanceSquared(unit.Position, start)).ThenBy(unit => unit.NetId).FirstOrDefault();
            }

            if (_selected == null)
            {
                spell.CancelCast();
                return;
            }

            _selectedTargets = EligibleInRange(_selected.Position, TargetRadius, false).Append(_selected).Distinct().ToList();
            spell.CastInfo.Targets.Clear();
        }

        public void OnSpellPostCast(Spell spell)
        {
            if (_selected == null || !Eligible(_selected, true) || _owner.IsDead)
            {
                return;
            }

            var originalPosition = _owner.Position;
            var direction = _selected.Position - originalPosition;
            if (direction.LengthSquared() < 0.001f)
            {
                direction = new Vector2(_owner.Direction.X, _owner.Direction.Z);
            }

            direction = direction.LengthSquared() < 0.001f ? Vector2.UnitX : Vector2.Normalize(direction);
            var landing = FindLanding(_selected.Position, direction);
            YasuoCastHelpers.StopComboAnimation(_owner);
            _dummy = CreateSpell(_owner, "YasuoRDummySpell", 45);
            _dummy.CastInfo.SpellLevel = _owner.Spells[0].CastInfo.SpellLevel;
            _dummy.Cast(_owner.Position, _owner.Position, _owner);
            if (_owner.IsDead || _owner.IsToRemove())
            {
                return;
            }

            _elapsed = 0;
            uint sequenceVersion = unchecked(++_sequenceVersion);
            _center = _selected.Position;
            _damage = spell.SpellData.GetEffectAmount(1, spell.CastInfo.SpellLevel) + spell.SpellData.Coefficient * (_owner.Stats.AttackDamage.Total - _owner.Stats.AttackDamage.BaseValue);
            _sequenceBuff = AddBuff("YasuoRSequence", SuspensionTime, 1, spell, _owner, _owner, infiniteduration: true, parent: this);
            if (_sequenceBuff == null || _sequenceBuff.Remove || !_owner.HasBuff(_sequenceBuff) || sequenceVersion != _sequenceVersion)
            {
                RemoveOwned(_sequenceBuff);
                _sequenceBuff = null;
                return;
            }

            foreach (var unit in _selectedTargets.Where(unit => Eligible(unit, false)))
            {
                HoldTarget(unit, SuspensionTime);
                if (!IsExecuting || sequenceVersion != _sequenceVersion)
                {
                    return;
                }
            }

            if (!_held.Any(held => held.Unit == _selected))
            {
                CancelSequence();
                return;
            }

            var previousCast = _owner.GetCastSpell();
            if (previousCast != null && previousCast != spell)
            {
                previousCast.CancelCast();
            }

            if (_owner.IsAttacking && !_owner.HasAutoAttacked && _owner.AutoAttackSpell != null)
            {
                _owner.CancelAutoAttack(false, true);
            }

            if (!IsExecuting || sequenceVersion != _sequenceVersion)
            {
                return;
            }

            _owner.RemoveBuffsWithName("YasuoQ");
            _owner.RemoveBuffsWithName("YasuoQ3W");
            ClearIndicators();
            _owner.Stats.CurrentMana = _owner.Stats.ManaPoints.Total;
            AddBuff("YasuoRArmorPen", 15, 1, spell, _owner, _owner);
            TeleportTo(_owner, landing.X, landing.Y);
            _owner.StopMovement();
            FaceDirection(_selected.Position, _owner, true);
            CreateDashMissile(originalPosition);
            CreateClone(originalPosition);
            _particles.Add(AddParticleTarget(_owner, _owner, "Yasuo_base_R_slash_cas.troy", _owner, lifetime: SuspensionTime, boneNameHash: 6941860));
            foreach (var held in _held)
            {
                _particles.Add(AddParticleTarget(_owner, held.Unit, "Yasuo_Base_R_impact_tar.troy", held.Unit, lifetime: SuspensionTime, boneNameHash: 6941860));
            }

            PlayAnimation(_owner, "Spell4", scaleTime: 0, flags: (AnimationFlags)225);
            spell.SetSpellToggle(false);
        }

        private bool HoldTarget(AttackableUnit unit, float duration)
        {
            if (!IsExecuting)
            {
                return false;
            }

            var shield = unit.GetBuffs().FirstOrDefault(buff => !buff.Remove && buff.BuffType == BuffType.SPELL_SHIELD);
            RemoveOwned(shield);
            var held = new HeldTarget
            {
                Unit = unit
            };
            held.Stun = AddBuff("YasuoRStun", duration + 0.1f, 1, _spell, unit, _owner, parent: this);
            if (held.Stun == null || held.Stun.Remove || !unit.HasBuff(held.Stun) || !IsExecuting)
            {
                RemoveOwned(held.Stun);
                return false;
            }

            held.Combo = AddBuff("YasuoRKnockUpCombo", duration + 0.1f, 1, _spell, unit, _owner, parent: this);
            held.Hold = AddBuff("YasuoRKnockUpComboTar", duration, 1, _spell, unit, _owner, infiniteduration: true, parent: this);
            if (held.Hold == null || held.Hold.Remove || !unit.HasBuff(held.Hold) || !IsExecuting)
            {
                RemoveOwned(held.Hold);
                RemoveOwned(held.Stun);
                RemoveOwned(held.Combo);
                return false;
            }

            _held.Add(held);
            return true;
        }

        private Vector2 FindLanding(Vector2 target, Vector2 direction)
        {
            var landing = GetClosestTerrainExit(target + direction * BlinkOffset, _owner.PathfindingRadius);
            var turrets = EnumerateValidUnitsInRange(_owner, target, 2000, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectTurrets).OfType<BaseTurret>().ToList();
            var threat = turrets.OrderBy(turret => Vector2.DistanceSquared(landing, turret.Position)).FirstOrDefault(turret => Vector2.Distance(landing, turret.Position) < turret.Stats.Range.Total + turret.CollisionRadius + _owner.CollisionRadius);
            if (threat == null)
            {
                return landing;
            }

            var away = target - threat.Position;
            if (away.LengthSquared() < 0.001f)
            {
                return landing;
            }

            away = Vector2.Normalize(away);
            float reach = threat.Stats.Range.Total + threat.CollisionRadius + _owner.CollisionRadius + 1;
            float offset = MathF.Max(BlinkOffset, reach - Vector2.Distance(target, threat.Position));
            var safe = GetClosestTerrainExit(target + away * MathF.Min(offset, TargetRadius), _owner.PathfindingRadius);
            if (turrets.All(turret => Vector2.Distance(safe, turret.Position) >= turret.Stats.Range.Total + turret.CollisionRadius + _owner.CollisionRadius))
            {
                return safe;
            }

            return landing;
        }

        private void CreateDashMissile(Vector2 originalPosition)
        {
            var carrier = CreateSpell(_owner, "TempYasuoRMissile", 50, enableScripts: false);
            _rMissile = carrier.CreateCustomMissile(originalPosition, _selected.Position,
                new MissileParameters { Type = MissileType.Target, IsVisualOnly = true }, isForceCastingOrChannel: true,
                isOverrideCastPosition: true, customHeightOffset: 0, target: _selected);
        }

        private void CreateClone(Vector2 position)
        {
            _cloneHidden = false;
            _clone = AddMinion(_owner, _owner.Model, "TestCube", position, team: _owner.Team, skinId: _owner.SkinID, ignoreCollision: true,
                targetable: false, isWard: true, aiPaused: true, useSpells: false, direction: _owner.Direction);
            _clone.Stats.GoldGivenOnDeath.BaseValue = 0;
            _clone.Stats.ExpGivenOnDeath.BaseValue = 0;
            _clone.SetStatus(StatusFlags.CanAttack | StatusFlags.CanCast, false);
            _clone.SetAnimStates(new Dictionary<string, string> { { "DEATH", "SPELL3" } }, this);
            PushCharacterFade(_clone, 0.6f, 0);
            _cloneEffect = AddParticleTarget(_owner, _clone, "Yasuo_Base_R_CloneVFX.troy", _clone, lifetime: 0.5f);
            var distance = Vector2.Distance(position, _owner.Position);
            if (distance > 0.01f)
            {
                ForceMovement(_clone, "", _owner.Position, distance / 0.25f, 0, 1, 0, consideredAsCC: false,
                    movementOrdersType: ForceMovementOrdersType.CANCEL_ORDER);
            }

            PlayAnimation(_clone, "Spell3", scaleTime: 0, flags: (AnimationFlags)229);
            _cloneHideTimer = new GameScriptTimer(0.4f, HideClone);
            _owner.RegisterTimer(_cloneHideTimer);
            _cloneTimer = new GameScriptTimer(0.5f, RemoveClone);
            _owner.RegisterTimer(_cloneTimer);
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

            _elapsed += diff / 1000f;
            if (_elapsed < SuspensionTime)
            {
                float remaining = SuspensionTime - _elapsed;
                foreach (var unit in EligibleInRange(_center, TargetRadius, false).ToList())
                {
                    if (_held.Any(held => held.Unit == unit) || !HoldTarget(unit, remaining))
                    {
                        continue;
                    }

                    _particles.Add(AddParticleTarget(_owner, unit, "Yasuo_Base_R_impact_tar.troy", unit, lifetime: remaining, boneNameHash: 6941860));
                }

                return;
            }

            var sequenceBuff = _sequenceBuff;
            uint sequenceVersion = _sequenceVersion;
            _sequenceBuff = null;
            try
            {
                foreach (var held in _held.ToArray())
                {
                    if (_owner.IsDead || _owner.IsToRemove() || sequenceVersion != _sequenceVersion)
                    {
                        break;
                    }

                    ResolveTarget(held);
                }
            }
            finally
            {
                CleanupTargets(cancel: false);
                RemoveOwned(sequenceBuff);
                CleanupVisuals();
            }
        }

        internal void OnSequenceRemoved(Buff buff)
        {
            if (_sequenceBuff == buff)
            {
                CancelSequence();
            }
        }

        internal void OnTargetHoldRemoved(Buff buff)
        {
            if (!IsExecuting)
            {
                return;
            }

            var held = _held.FirstOrDefault(target => target.Hold == buff);
            if (held == null || held.Resolved)
            {
                return;
            }

            ResolveTarget(held);
            RemoveOwned(held.Stun);
            RemoveOwned(held.Combo);
        }

        private void ResolveTarget(HeldTarget held)
        {
            if (held.Resolved)
            {
                return;
            }

            held.Resolved = true;
            if (held.Unit.IsDead || held.Unit.IsToRemove())
            {
                return;
            }

            held.Unit.TakeDamage(_owner, _damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, _spell);
            AddParticleTarget(_owner, held.Unit, "Yasuo_Base_R_land_tar.troy", held.Unit, lifetime: 0.5f);
        }

        private static void RemoveOwned(Buff buff)
        {
            if (buff != null && !buff.Remove && buff.TargetUnit.HasBuff(buff))
            {
                RemoveBuff(buff);
            }
        }

        private void CleanupTargets(bool cancel)
        {
            var targets = _held.ToArray();
            _held.Clear();
            foreach (var held in targets)
            {
                RemoveOwned(held.Hold);
                if (cancel)
                {
                    RemoveOwned(held.Combo);
                    RemoveOwned(held.Stun);
                }
            }
        }

        private void HideClone()
        {
            _cloneHideTimer?.EndTimerWithoutCallback();
            _cloneHideTimer = null;
            if (_clone == null || _cloneHidden)
            {
                return;
            }

            _cloneHidden = true;
            PushCharacterFade(_clone, 0, 0);
            TeleportTo(_clone, 1800, 1900);
            if (_cloneEffect != null)
            {
                RemoveParticle(_cloneEffect);
            }

            _cloneEffect = null;
        }

        private void RemoveClone()
        {
            _cloneTimer?.EndTimerWithoutCallback();
            _cloneTimer = null;
            HideClone();
            if (_clone != null)
            {
                DespawnMinion(_clone);
            }

            _clone = null;
        }

        private void CleanupVisuals()
        {
            StopAnimation(_owner, "Spell4", fade: true);
            foreach (var particle in _particles)
            {
                RemoveParticle(particle);
            }

            _particles.Clear();
            if (_rMissile != null && !_rMissile.IsToRemove())
            {
                _rMissile.SetToRemove();
            }

            _rMissile = null;
            RemoveClone();
        }

        public void CancelSequence()
        {
            unchecked
            {
                _sequenceVersion++;
            }

            var buff = _sequenceBuff;
            _sequenceBuff = null;
            CleanupTargets(cancel: true);
            RemoveOwned(buff);
            CleanupVisuals();
            _selectedTargets = null;
            _selected = null;
        }

        private void ClearIndicators()
        {
            foreach (var effect in _indicators.Values)
            {
                RemoveParticle(effect);
            }

            _indicators.Clear();
        }

        private void OnDeath(DeathData data)
        {
            ClearIndicators();
            CancelSequence();
        }

        public void OnUpdate(float diff)
        {
            _spell.SetToolTipVar(0, _spell.GetCooldown((byte)Math.Max(1, (int)_spell.CastInfo.SpellLevel)));
            _spell.SetToolTipVar(1, _spell.GetCooldown((byte)(_spell.CastInfo.SpellLevel + 1)));
            var targets = _owner.IsDead ? new List<AttackableUnit>() : EligibleInRange(_owner.Position, _spell.GetCurrentCastRange(), true).ToList();
            bool hasTargets = targets.Count != 0;
            _spell.SetIconIndex(hasTargets ? (byte)0 : (byte)1);
            bool available = !IsExecuting
                && !_owner.IsDead
                && _spell.CastInfo.SpellLevel > 0
                && _spell.State == SpellState.STATE_READY
                && _owner.CanCastByStatus(_spell)
                && hasTargets;
            if (_spell.Toggle != available)
            {
                _spell.SetSpellToggle(available);
            }

            var marked = available ? targets : new List<AttackableUnit>();
            foreach (var unit in _indicators.Keys.Where(unit => !marked.Contains(unit)).ToList())
            {
                RemoveParticle(_indicators[unit]);
                _indicators.Remove(unit);
            }

            foreach (var unit in marked.Where(unit => !_indicators.ContainsKey(unit)))
            {
                _indicators[unit] = AddParticleTarget(_owner, unit, "Yasuo_base_R_indicator_beam.troy", _owner, lifetime: -1,
                    reqVision: false, unitOnly: _owner, flags: FXFlags.TargetDirection);
            }
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ClearIndicators();
            CancelSequence();
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class YasuoRDummySpell : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true
        };

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            spell.CastInfo.TargetPositionEnd = Vector3.Zero;
        }
    }
}
