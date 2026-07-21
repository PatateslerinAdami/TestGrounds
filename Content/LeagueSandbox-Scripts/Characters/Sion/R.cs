using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class SionR : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            // ChargeDuration is resolved at runtime by GetEffectiveChannelDuration from
            // SionR.json ChannelDuration = 8.0 (SpellTargeter blocks have no RangeGrowthDuration).
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };

        private ObjAIBase _sion;
        private Spell _spell;
        private Buff _buff;
        private AttackableUnit _closestUnit;
        private float _currentAngle;
        private float _targetAngle;

        private float _unitHitboxRadius = 160f;
        private float _collisionGracePeriod = 0.1f;

        // Final-leap parameters measured from replay bae83ecc (Sion netid 1073741857, leap at t=1108417):
        // a force-move with gravity 0, ~268 world-units at speed 605 (the leap's 0x64 WaypointGroupWithSpeed).
        // The leap fires ONLY on clean release/timeout — a wall/champion collision slams in place with NO
        // force-move (the collided-branch in OnUpdate FireCharges at the current position; replay-confirmed).
        private const float LeapDistance = 268f;
        private const float LeapSpeed = 605f;

        private float _chargeTime;
        private float _waypointUpdateTimer;

        // Guards against StopCharge re-entrancy: the hitChampion/hitWall branches call
        // StopChanneling, which publishes OnSpellChargeCancel and re-enters StopCharge(false, false).
        private bool _chargeStopped;


        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
            _spell = spell;
        }

        public void OnSpellPostCast(Spell spell)
        {
            _sion.StopMovement();
        }

        public void OnSpellChargeStart(Spell spell)
        {
            _buff = AddBuff("SionR", 8.5f, 1, spell, _sion, _sion);
            _chargeTime = 0f;
            _waypointUpdateTimer = 0f;
            // Reset the re-entry guard per cast — the script instance is reused across casts, so
            // without this every cast after the first would short-circuit in StopCharge.
            _chargeStopped = false;

            // Lock/steer the caster's camera for the charge (replay-verified, distance 900).
            LockCamera(_sion, true);

            _sion.IgnoreMoveOrders = true;
            spell.SpellData.CanMoveWhileChanneling = true;

            Vector2 dir =
                Vector2.Normalize(new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z) -
                                  _sion.Position);
            _currentAngle = (float)Math.Atan2(dir.Y, dir.X);
            _targetAngle = _currentAngle;
        }

        public void OnSpellChargeUpdate(Spell spell, Vector3 position, bool forceStop)
        {
            Vector2 targetDir = Vector2.Normalize(new Vector2(position.X, position.Z) - _sion.Position);
            _targetAngle = (float)Math.Atan2(targetDir.Y, targetDir.X);
        }

        public void OnSpellChargeTick(Spell spell, float diff)
        {
            if (_sion.IsDead) return;

            float deltaSeconds = diff / 1000f;
            _chargeTime += deltaSeconds;

            float angleDiff = _targetAngle - _currentAngle;

            while (angleDiff > Math.PI) angleDiff -= (float)(2 * Math.PI);
            while (angleDiff < -Math.PI) angleDiff += (float)(2 * Math.PI);

            float turnRate = 0.4f;
            float step = turnRate * deltaSeconds;

            if (Math.Abs(angleDiff) <= step)
            {
                _currentAngle = _targetAngle;
            }
            else
            {
                _currentAngle += Math.Sign(angleDiff) * step;
            }

            while (_currentAngle > Math.PI) _currentAngle -= (float)(2 * Math.PI);
            while (_currentAngle < -Math.PI) _currentAngle += (float)(2 * Math.PI);

            Vector2 newDir = new Vector2((float)Math.Cos(_currentAngle), (float)Math.Sin(_currentAngle));

            if (_chargeTime > _collisionGracePeriod)
            {
                Vector2 checkPos = _sion.Position + newDir * _sion.CollisionRadius;
                if (!IsWalkable(checkPos.X, checkPos.Y, 10f))
                {
                    spell.FireCharge(_sion.Position);
                    StopCharge(false, true);
                }
                else
                {
                    var nearbyUnits = GetUnitsInRange(_sion, _sion.Position, _unitHitboxRadius + 100f, true,
                        SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectTurrets);
                    if ((from unit in nearbyUnits
                            let dist = Vector2.Distance(_sion.Position, unit.Position)
                            where dist <= _unitHitboxRadius + unit.CollisionRadius
                            select unit).FirstOrDefault() != null)
                    {
                        spell.FireCharge(_sion.Position);
                        StopCharge(true, false);
                    }
                }
            }

            _waypointUpdateTimer -= diff;
            if (_waypointUpdateTimer <= 0f)
            {
                // Bug fix: ObjAIBase.Move() only advances Position when MoveOrder is a "moving"
                // order; a stationary caster (fresh spawn = OrderNone, post-leap = Stop, or
                // auto-attacking = CastSpell) hits Move()'s early-return, so the server never walks
                // the charge path while the client does — then every re-broadcast snaps the client
                // back to the (un-moved) start position. Force MoveTo each tick so the server
                // actually advances. Set every tick to also override the AttackTo->CastSpell rewrite
                // that StartChanneling applies at channel start. publish:false = direct field set.
                _sion.UpdateMoveOrder(OrderType.MoveTo, false);
                Vector2 newPos = _sion.Position + newDir * 500f;
                _sion.SetWaypoints(new List<Vector2> { _sion.Position, newPos }, true);
                _waypointUpdateTimer = 100f;
            }
        }

        public void OnSpellChargeCancel(Spell spell, ChannelingStopSource reason)
        {
            StopCharge(false, false);
            spell.FireCharge(_sion.Position);
        }

        public void OnSpellChargeFire(Spell spell)
        {
            // Recast or duration timeout — both trigger the slam after the forward leap.
            // Clear charge HUD; impact lands at the leap endpoint.
            Vector2 dir2D = new Vector2((float)Math.Cos(_currentAngle), (float)Math.Sin(_currentAngle));
            spell.FireCharge(_sion.Position + dir2D * LeapDistance);
            StopCharge(false, false);
        }

        private void StopCharge(bool hitChampion, bool hitWall = false)
        {
            if (_chargeStopped) return;
            _chargeStopped = true;

            // Release the camera lock now the charge has ended (cancel / recast / timeout / collision).
            LockCamera(_sion, false);

            _sion.StopMovement();
            _sion.IgnoreMoveOrders = false;

            _buff.SetToExpired();
            RemoveBuff(_buff);

            //recast or timeout
            if (!hitChampion && !hitWall)
            {
                Vector2 dir2D = new Vector2((float)Math.Cos(_currentAngle), (float)Math.Sin(_currentAngle));

                _sion.RegisterTimer(new GameScriptTimer(0.10f, () =>
                {
                    if (_sion.IsDead) return;
                    PlayAnimation(_sion, "Spell4_STOP", 0, 0, 1, flags: AnimationFlags.Lock | AnimationFlags.NoBlend |
                                                                        AnimationFlags.FreezeAtEnd |
                                                                        AnimationFlags.Junk5 |
                                                                        AnimationFlags.Junk6 | AnimationFlags.Junk7);
                    ApiEventManager.OnMoveEnd.AddListener(this, _sion, OnMoveEnd);
                    ForceMove(_sion, _sion.Position + dir2D * LeapDistance, LeapSpeed, gravity: 0f,
                        facing: ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION,
                        resolve: ForceMovementType.FIRST_COLLISION_HIT, orders: ForceMovementOrdersType.CANCEL_ORDER,
                        movementName: "SionRLeap");
                }));
            }
            else if (hitWall)
            {
                if (_sion.ChannelSpell == _spell)
                {
                    _sion.StopChanneling(ChannelingStopCondition.Cancel,
                        ChannelingStopSource.StunnedOrSilencedOrTaunted);
                }

                PlayAnimation(_sion, "Spell4", 0, 0, 1,
                    AnimationFlags.NoBlend | AnimationFlags.Junk5 | AnimationFlags.Junk7);
                _spell.CastInfo.InstanceVars.Set("hasLeaped", false);
                AddBuff("Stun", _spell.SpellData.EffectLevelAmount[5][_spell.CastInfo.SpellLevel], 1, _spell, _sion,
                    _sion);
                // Deal the slam damage in place — previously this only landed via the (now suppressed)
                // re-entrant leap path, so without this explicit call a wall collision would deal no damage.
                OnHit();
            }
            else if (hitChampion)
            {
                if (_sion.ChannelSpell == _spell)
                {
                    _sion.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.LostTarget);
                }

                _spell.CastInfo.InstanceVars.Set("hasLeaped", false);
                PlayAnimation(_sion, "Spell4_Hit", 0, 0, 1,
                    AnimationFlags.NoBlend | AnimationFlags.Junk5 | AnimationFlags.Junk7);
                OnHit();
            }
        }

        private void OnMoveEnd(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters.MovementName != "SionRLeap") return;
            PlayAnimation(_sion, "Spell4", 0, 0, 1,
                AnimationFlags.NoBlend | AnimationFlags.Junk5 | AnimationFlags.Junk7);
            _spell.CastInfo.InstanceVars.Set("hasLeaped", true);
            OnHit();
            ApiEventManager.OnMoveEnd.RemoveListener(this, _sion, OnMoveEnd);
        }

        private void OnHit()
        {
            if (_spell.CastInfo.InstanceVars.Get("hasLeaped", true))
            {
                var impactPos = _sion.Position + new Vector2(_sion.Direction.X, _sion.Direction.Z) * 200;
                SpellEffectCreate("Sion_Base_R_Explosion.troy", _sion, null, null, impactPos, scale: 1.1f,
                    lifetime: 0.5f, flags: FXFlags.SimulateWhileOffScreen);

                var unitsInRange = GetUnitsInRange(_sion, impactPos, 400f, true,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                    SpellDataFlags.AffectHeroes);
                foreach (var unit in unitsInRange)
                {
                    SpellEffectCreate("Sion_Base_R_Tar.troy", _sion, unit, unit, flags: FXFlags.SimulateWhileOffScreen);
                    var ad = _sion.Stats.AttackDamage.Total * _spell.SpellData.Coefficient;
                    var dmg = _spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] + ad;
                    unit.TakeDamage(_sion, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                        false);
                }
            }
            else
            {
                SpellEffectCreate("Sion_Base_R_Explosion.troy", _sion, null, null, _sion.Position, scale: 1.1f,
                    lifetime: 0.5f, flags: FXFlags.SimulateWhileOffScreen);
                var unitsInRange = GetUnitsInRange(_sion, _sion.Position, 400f, true,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                    SpellDataFlags.AffectHeroes);
                foreach (var unit in unitsInRange)
                {
                    SpellEffectCreate("Sion_Base_R_Tar.troy", _sion, unit, unit, flags: FXFlags.SimulateWhileOffScreen);
                    if (Vector2.Distance(_sion.Position, unit.Position) > 350f)
                    {
                        AddBuff("SionRSlow", 3f, 1, _spell, unit, _sion);
                    }
                    else
                    {
                        AddBuff("SionRTarget", _spell.SpellData.EffectLevelAmount[5][_spell.CastInfo.SpellLevel], 1,
                            _spell, unit, _sion);
                        AddBuff("Stun", _spell.SpellData.EffectLevelAmount[6][_spell.CastInfo.SpellLevel], 1, _spell,
                            unit, _sion);
                    }

                    var ad = _sion.Stats.AttackDamage.Total * _spell.SpellData.Coefficient;
                    var dmg = _spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] + ad;
                    unit.TakeDamage(_sion, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                        false);
                }
            }

            _sion.RegisterTimer(new GameScriptTimer(0.10f,
                () =>
                {
                    StopAnimation(_sion, "Spell4_STOP", StopAnimationFlags.FadeOut | StopAnimationFlags.IgnoreLock);
                }));
        }
    }


    public class SionVOModeChange : ISpellScript
    {
        private ObjAIBase _sion;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false,
        };


        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
        }

        public void OnSpellCast(Spell spell)
        {
        }

        public void OnSpellPostCast(Spell spell)
        {
        }
    }
}