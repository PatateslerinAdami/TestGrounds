using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using Spells;
using System.Collections.Generic;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class FizzPlayful : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            IsHidden = true,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Buff _buff;
        private ObjAIBase _owner;
        private Spell _source, _flash;
        private ForceMovementParameters _movement;
        private uint _flashCastId;
        private byte _teleportId, _rank;
        private float _flashLandingTime, _damage, _slow, _cooldown, _elapsed;
        private Particle _jump, _weapon, _idle;
        private Fade _fade;
        private Vector2? _buffer;
        private bool _switching, _ending, _second, _landed, _transition, _awaitingFlash;
        private bool _restore = true;

        public bool CanRecast
        {
            get
            {
                return !_ending && _landed && !_second;
            }
        }

        public float NativeCooldown
        {
            get
            {
                return _cooldown;
            }
        }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            _owner = (ObjAIBase)unit;
            _source = ownerSpell;
            _rank = ownerSpell.CastInfo.SpellLevel;
            _damage = ownerSpell.SpellData.GetEffectAmount(1, _rank) + ownerSpell.SpellData.Coefficient * _owner.Stats.AbilityPower.Total;
            _slow = ownerSpell.SpellData.GetEffectAmount(2, _rank) / 100;
            _cooldown = ownerSpell.GetCooldown();
            buff.SetStatusEffect(StatusFlags.Targetable, false);
            FizzHelpers.CancelAttack(_owner);
            ApiEventManager.OnDeath.AddListener(this, _owner, _ => RemoveBuff(buff));
            ApiEventManager.OnMoveSuccess.AddListener(this, _owner, OnSuccess);
            ApiEventManager.OnMoveFailure.AddListener(this, _owner, OnFailure);
            _flash = _owner.GetSpell("SummonerFlash");
            _flashCastId = _flash?.CastInfo.SpellNetID ?? 0;
            if (_flash != null)
            {
                ApiEventManager.OnSpellPress.AddListener(this, _flash, OnFlash);
            }

            _jump = AddParticleTarget(_owner, _owner, "Fizz_Jump_cas.troy", _owner, lifetime: 3, bone: "root");
            _weapon = AddParticleTarget(_owner, _owner, "Fizz_Jump_WeaponGlow.troy", _owner, lifetime: 3, bone: "BUFFBONE_CSTM_WEAPON_1");
            _fade = _owner.FadeOut(0.7f, 50);
            var end = FizzHelpers.Clamp(_owner, FizzHelpers.Aim(ownerSpell), 400);
            Swap("FizzJumpBuffer", 0.15f);
            StartMovement(end, 0.35f, Vector2.Distance(_owner.Position, end) > 100 ? 40 : 20, "Spell3a", 0.75f, true);
        }

        private void Swap(string name, float cooldown)
        {
            _switching = true;
            try
            {
                var spell = _owner.SetSpell(name, 2, true, networkOld: true);
                spell.SetManaCostOverride(name == "FizzJump" ? null : 0);
                spell.SetCooldown(cooldown, true, cooldown);
            }
            finally
            {
                _switching = false;
            }
        }

        private void StartMovement(Vector2 end, float duration, float gravity, string animation, float scale, bool allowRecast)
        {
            var previous = _movement;
            _movement = null;
            _transition = true;
            if (previous != null && _owner.MovementParameters == previous)
            {
                CancelDash(_owner);
            }

            _transition = false;
            end = GetClosestTerrainExit(end, _owner.PathfindingRadius + 1);
            var movement = new ForceMovementParameters
            {
                MovementName = "FizzJump",
                Caster = _owner,
                TargetPosition = end,
                ParabolicStartPoint = _owner.Position,
                Duration = duration,
                PathSpeedOverride = MathF.Max(0.001f, Vector2.Distance(_owner.Position, end) / duration),
                ParabolicGravity = gravity,
                SetStatus = StatusFlags.CanAttack | StatusFlags.CanCast,
                AllowedSpellSlots = allowRecast ? new HashSet<byte>() { 2, 4, 5 } : new HashSet<byte>() { 4, 5 },
                BlockedSpellSlots = allowRecast ? new HashSet<byte>() { 0, 1, 3 } : new HashSet<byte>() { 0, 1, 2, 3 },
                BufferSpellCasts = false,
                MovementOrdersType = ForceMovementOrdersType.CANCEL_ORDER,
                Animation = animation,
                AnimationFlags = (AnimationFlags)128,
                AnimationTimeScale = scale,
                AnimationSpeedScale = 1
            };
            _movement = movement;
            _owner.StartForcedMovement(movement);
            if (_owner.MovementParameters != movement)
            {
                RemoveBuff(_buff);
            }
        }

        public void Buffer(Vector2 aim)
        {
            if (!_ending && !_second)
            {
                _buffer = aim;
            }
        }

        public void Recast(Vector2 aim)
        {
            if (!CanRecast)
            {
                return;
            }

            _second = true;
            AddParticleTarget(_owner, _owner, "fizz_playfultrickster_flip_sound.troy", _owner, lifetime: 0.5f, bone: "root");
            RemoveOverrideAnimations(_owner, this);
            RemoveParticle(_idle);
            _idle = null;
            StartMovement(FizzHelpers.Clamp(_owner, aim, 270), 0.325f, 30, "Spell3d", 1, false);
        }

        private void OnSuccess(AttackableUnit unit, ForceMovementParameters movement)
        {
            if (movement != _movement || _ending)
            {
                return;
            }

            _movement = null;
            if (_second)
            {
                Land(true);
                return;
            }

            if (_landed)
            {
                Land(false);
                return;
            }

            _landed = true;
            _owner.FadeIn(_fade, 0);
            _fade = null;
            Swap("FizzJumpTwo", 0);
            OverrideAnimation(_owner, "SPELL3B", "IDLE1", this);
            _idle = AddParticleTarget(_owner, _owner, "fizz_playfultrickster_idle_sound.troy", _owner, lifetime: 1, bone: "root");
            StartMovement(_owner.Position, MathF.Max(0.01f, 0.75f - _elapsed), 0, "", 0, true);
            if (_buffer.HasValue)
            {
                _owner.Spells[2].Cast(_buffer.Value, _buffer.Value, null);
            }
        }

        private void OnFailure(AttackableUnit unit, ForceMovementParameters movement)
        {
            if (movement == _movement && !_transition && !_ending)
            {
                _movement = null;
                if (_flash != null && _flash.CastInfo.SpellNetID != _flashCastId)
                {
                    _flashCastId = _flash.CastInfo.SpellNetID;
                    _teleportId = _owner.TeleportID;
                    _flashLandingTime = MathF.Max(0.001f, movement.Duration - movement.ElapsedTime / 1000);
                    _awaitingFlash = true;
                    return;
                }

                RemoveBuff(_buff);
            }
        }

        private void OnFlash(Spell spell, SpellCastInfo input)
        {
            _flashCastId = spell.CastInfo.SpellNetID;
            if (!_awaitingFlash)
            {
                return;
            }

            _awaitingFlash = false;
            if (!input.CouldCast || input.WasQueued || _owner.TeleportID == _teleportId
                || _owner.MovementParameters != null || _owner.IsDead || _owner.IsToRemove())
            {
                RemoveBuff(_buff);
                return;
            }

            StartMovement(_owner.Position, _flashLandingTime, 0, "", 0, !_second);
        }

        public void OnUpdate(float diff)
        {
            _elapsed += diff / 1000;
            if (_awaitingFlash || _owner.IsDead || _owner.IsToRemove())
            {
                RemoveBuff(_buff);
                return;
            }

            if (_landed && !_second && _elapsed >= 0.75f)
            {
                Land(false);
            }
        }

        private void Land(bool second)
        {
            if (_ending)
            {
                return;
            }

            _ending = true;
            RemoveBuff(_buff);
            if (_owner.IsDead || _owner.IsToRemove())
            {
                return;
            }

            if (!second)
            {
                var variables = new BuffVariables();
                variables.Set("damage", _damage);
                variables.Set("slow", _slow);
                AddBuff("FizzLandingRecovery", 0.5f, 1, _source, _owner, _owner, buffVariables: variables);
                AddBuff("FizzTrickSlamSoundDummy", 2, 1, _source, _owner, _owner);
            }
            else
            {
                Impact(_owner, _source, _damage, _slow, true);
            }
        }

        internal static void Impact(ObjAIBase owner, Spell source, float damage, float slow, bool second)
        {
            AddParticleTarget(owner, owner, second ? "Fizz_TrickSlamTwo.troy" : "Fizz_TrickSlam.troy", owner, lifetime: 0.75f);
            foreach (var target in GetUnitsInRange(owner, owner.Position, second ? 270 : 330, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes))
            {
                if (!FizzHelpers.Eligible(owner, target))
                {
                    continue;
                }

                target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, source);
                AddParticleTarget(owner, target, "Fizz_TrickSlam_tar.troy", target, lifetime: 0.5f);
                if (!second && !target.IsDead)
                {
                    var vars = new BuffVariables();
                    vars.Set("slow", slow);
                    AddBuff("FizzWSlow", 2, 1, source, target, owner, buffVariables: vars);
                }
            }
        }

        public void SpellRemoved(Spell spell)
        {
            if (!_switching && !_ending && _owner.Spells[2] == spell)
            {
                _restore = false;
                RemoveBuff(_buff);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _ending = true;
            var movement = _movement;
            _movement = null;
            if (movement != null && _owner.MovementParameters == movement)
            {
                CancelDash(_owner);
            }

            RemoveOverrideAnimations(_owner, this);
            RemoveParticle(_jump);
            RemoveParticle(_weapon);
            RemoveParticle(_idle);
            if (_fade != null)
            {
                _owner.FadeIn(_fade, 0);
            }

            StopAnimation(_owner, _second ? "Spell3d" : "Spell3a", fade: true);
            if (_restore && !_owner.IsToRemove())
            {
                Swap("FizzJump", _cooldown);
            }

            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class FizzLandingRecovery : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            IsHidden = true,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Buff _buff;
        private bool _hit;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            buff.SetStatusEffect(StatusFlags.CanAttack, false);
            PlayAnimation(
                unit,
                "Spell3c",
                0,
                0,
                1,
                (AnimationFlags)129
            );
            PlayAnimation(
                unit,
                "Spell3c",
                0,
                0,
                1,
                (AnimationFlags)132
            );
            ApiEventManager.OnDeath.AddListener(this, unit, _ => RemoveBuff(buff));
        }

        public void OnUpdate(float diff)
        {
            if (_hit || _buff.Remove || _buff.TimeElapsed < _buff.Duration)
            {
                return;
            }

            _hit = true;
            var owner = (ObjAIBase)_buff.TargetUnit;
            if (owner.IsDead || owner.IsToRemove())
            {
                return;
            }

            FizzPlayful.Impact(owner, _buff.OriginSpell, _buff.Variables.GetFloat("damage"), _buff.Variables.GetFloat("slow"), false);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            StopAnimation(unit, "Spell3c", fade: true);
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class FizzTrickSlamSoundDummy : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            IsHidden = true,
            SendHiddenToClient = true,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();
    }

    public class FizzWSlow : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.SLOW,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            StatsModifier.MoveSpeed.PercentBonus = -buff.Variables.GetFloat("slow");
            unit.AddStatModifier(StatsModifier);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.RemoveStatModifier(StatsModifier);
        }
    }
}
