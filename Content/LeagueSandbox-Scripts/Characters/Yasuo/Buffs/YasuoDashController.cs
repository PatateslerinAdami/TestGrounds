using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoDashController : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; private set; }

        private SpellSector _targetZone;
        private Buff _buff;
        private bool doSpin, knockUp, _awaitingFlash;
        private ObjAIBase owner;
        private Spell origSpell;
        private const StatusFlags Knockdown = StatusFlags.Stunned | StatusFlags.Rooted | StatusFlags.Feared | StatusFlags.Charmed | StatusFlags.Taunted | StatusFlags.Suppressed;
        private ForceMovementParameters _movement;
        private Spell _flash;
        private byte _teleportId;
        private uint _flashCastId;
        private GameScriptTimer _spinStopTimer;

        public bool IsActive
        {
            get
            {
                return _movement != null && owner.MovementParameters == _movement;
            }
        }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            doSpin = false;
            _buff = buff;
            owner = ownerSpell.CastInfo.Owner;
            ApiEventManager.OnDeath.AddListener(this, owner, OnDeath);
            ApiEventManager.OnMoveSuccess.AddListener(this, owner, OnMoveSuccess);
            ApiEventManager.OnMoveFailure.AddListener(this, owner, OnMoveFailure);
            origSpell = owner.Spells[0];
            _flash = owner.GetSpell("SummonerFlash");
            _flashCastId = _flash?.CastInfo.SpellNetID ?? 0;
            if (_flash != null)
            {
                ApiEventManager.OnSpellPress.AddListener(this, _flash, OnSpellPress2);
            }

            var target = buff.Variables.Get<AttackableUnit>("target");
            AddParticleTarget(owner, owner, "Yasuo_Base_E_Dash", owner);
            AddParticleTarget(owner, target, "Yasuo_Base_E_dash_hit", target);
            var direction = target.Position - owner.Position;
            if (direction.LengthSquared() < 0.001f)
            {
                direction = new Vector2(owner.Direction.X, owner.Direction.Z);
            }

            var to = direction.LengthSquared() > 0.001f ? Vector2.Normalize(direction) : Vector2.UnitX;
            var movement = new ForceMovementParameters
            {
                TargetPosition = owner.Position + to * 475f,
                ParabolicStartPoint = owner.Position,
                PathSpeedOverride = 750f + owner.Stats.GetTrueMoveSpeed() * 0.6f,
                Animation = "Spell3",
                AnimationTimeScale = 0,
                AnimationSpeedScale = 1,
                AnimationFlags = (AnimationFlags)229,
                DoStopAnimation = true,
                StopAnimationFlags = StopAnimationFlags.Fade | StopAnimationFlags.IgnoreLock,
                MovementName = "YasuoE",
                SetStatus = StatusFlags.CanAttack,
                Caster = owner,
                BufferSpellCasts = true
            };
            movement.BlockedSpellSlots.Add(2);
            owner.StartForcedMovement(movement);
            if (owner.MovementParameters == movement)
            {
                _movement = movement;
            }
            else
            {
                RemoveBuff(buff);
            }
        }

        public void ArmCombo(Spell spell)
        {
            if (!IsActive || doSpin)
            {
                return;
            }

            origSpell = spell;
            knockUp = spell.SpellName == "YasuoQ3" || spell.SpellName == "YasuoQ3W";
            doSpin = true;
            _movement.BlockedSpellSlots.Add(0);
            _movement.BlockedSpellSlots.Add(1);
        }

        public void StopComboAnimation()
        {
            var stopTimer = _spinStopTimer;
            _spinStopTimer = null;
            if (stopTimer == null)
            {
                return;
            }

            stopTimer.EndTimerWithoutCallback();
            StopAnimation(owner, "Spell1_Dash", fade: true);
            RemoveBuff(_buff);
        }

        private void RemoveMovementListeners()
        {
            ApiEventManager.OnMoveSuccess.RemoveListener(this, owner, OnMoveSuccess);
            ApiEventManager.OnMoveFailure.RemoveListener(this, owner, OnMoveFailure);
        }

        private void OnMoveFailure(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters != _movement)
            {
                return;
            }

            RemoveMovementListeners();
            _awaitingFlash = doSpin && _flash != null && _flash.CastInfo.SpellNetID != _flashCastId;
            if (!_awaitingFlash)
            {
                doSpin = false;
            }

            _teleportId = owner.TeleportID;
            _movement = null;
            if (!_awaitingFlash)
            {
                RemoveBuff(_buff);
            }
        }

        public void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters != _movement)
            {
                return;
            }

            RemoveMovementListeners();
            _movement = null;
            ResolveCombo(unit);
            if (_flash != null)
            {
                ApiEventManager.OnSpellPress.RemoveListener(this, _flash, OnSpellPress2);
            }

            if (_spinStopTimer == null)
            {
                RemoveBuff(_buff);
            }
        }

        private void ResolveCombo(AttackableUnit unit)
        {
            if (!doSpin)
            {
                return;
            }

            if (owner.IsDead)
            {
                return;
            }

            if ((owner.Status & Knockdown) != 0)
            {
                return;
            }

            if (!owner.CanCast(owner.Spells[0]))
            {
                return;
            }

            doSpin = false;
            Spells.YasuoCastHelpers.ResetSheath(origSpell);
            {
                if (knockUp)
                {
                    AddParticleTarget(owner, owner, "yasuo_base_eq3_cas", owner);
                    owner.RemoveBuffsWithName("YasuoQ3W");
                }

                AddParticleTarget(owner, owner, "yasuo_base_eq_cas", owner);
                AddBuff(
                    "YasuoEQComboAnimLock",
                    YasuoEQComboAnimLock.Duration,
                    1,
                    origSpell,
                    owner,
                    owner
                );
                PlayAnimation(owner, "Spell1_Dash", scaleTime: 0, flags: (AnimationFlags)229);
                _spinStopTimer = new GameScriptTimer(YasuoEQComboAnimLock.Duration, StopComboAnimation);
                owner.RegisterTimer(_spinStopTimer);
                ApiEventManager.OnCreateSector.AddListener(this, origSpell, OnComboSectorCreated, true);
                _targetZone = origSpell.CreateSpellSector(new SectorParameters
                {
                    Type = SectorType.Area,
                    BindObject = owner,
                    Length = 200f,
                    Tickrate = 0,
                    SingleTick = true,
                    OverrideFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral
                        | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes
                });
                ApiEventManager.OnCreateSector.RemoveListener(this, origSpell, OnComboSectorCreated);
                ApiEventManager.OnSpellSectorHit.RemoveListener(this, _targetZone, OnTargetZoneHit);
            }
        }

        private void OnComboSectorCreated(Spell spell, SpellSector sector)
        {
            ApiEventManager.OnSpellSectorHit.AddListener(this, sector, OnTargetZoneHit, true);
        }

        public void OnTargetZoneHit(SpellSector sector, AttackableUnit target)
        {
            var enemies = EnumerateValidUnitsInRange(
                owner,
                owner.Position,
                200f,
                true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes
                    | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral
            ).ToList();
            if (enemies.Count != 0)
            {
                if (owner.HasBuff("YasuoQ"))
                {
                    owner.RemoveBuffsWithName("YasuoQ");
                    AddBuff(
                        "YasuoQ3W",
                        9f,
                        1,
                        origSpell,
                        owner,
                        owner
                    );
                }
                else if (!knockUp)
                {
                    AddBuff(
                        "YasuoQ",
                        3f,
                        1,
                        origSpell,
                        owner,
                        owner
                    );
                }

                if (knockUp)
                {
                    PlaySound("Play_sfx_Yasuo_YasuoQ3W_hit", owner);
                }
                else
                {
                    PlaySound("Play_sfx_Yasuo_YasuoQ_hit", owner);
                }
            }

            bool firstTarget = true;
            foreach (var enemy in enemies.OrderBy(enemy => Vector2.DistanceSquared(owner.Position, enemy.Position)))
            {
                Spells.YasuoCastHelpers.ApplyQDamage(origSpell, enemy, firstTarget);
                firstTarget = false;
                if (knockUp)
                {
                    AddBuff(
                        Spells.YasuoCastHelpers.KnockupBuffName(enemy),
                        Spells.YasuoCastHelpers.KnockupDuration,
                        1,
                        origSpell,
                        enemy,
                        owner
                    );
                }
            }
        }

        public void OnSpellPress2(Spell sp, SpellCastInfo sc)
        {
            if (!_awaitingFlash)
            {
                return;
            }

            bool resolve = _awaitingFlash
                && sc.CouldCast
                && !sc.WasQueued
                && owner.TeleportID != _teleportId
                && owner.MovementParameters == null;
            _awaitingFlash = false;
            if (resolve)
            {
                ResolveCombo(owner);
            }

            if (_flash != null)
            {
                ApiEventManager.OnSpellPress.RemoveListener(this, _flash, OnSpellPress2);
            }

            if (_spinStopTimer == null)
            {
                RemoveBuff(_buff);
            }
        }

        public void OnUpdate(float diff)
        {
            if (owner.IsDead || owner.IsToRemove())
            {
                RemoveBuff(_buff);
                return;
            }

            _awaitingFlash = false;
            if (!IsActive)
            {
                if (_spinStopTimer == null)
                {
                    RemoveBuff(_buff);
                }

                return;
            }

            if ((owner.Status & Knockdown) != 0)
            {
                CancelDash(owner);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            StopComboAnimation();
            ApiEventManager.RemoveAllListenersForOwner(this);
            if (IsActive)
            {
                CancelDash(owner);
            }

            _movement = null;
            doSpin = false;
        }

        private void OnDeath(DeathData data)
        {
            RemoveBuff(_buff);
        }
    }
}
