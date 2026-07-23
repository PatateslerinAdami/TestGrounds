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
        private bool _instantHit = false;

        private float _unitHitboxRadius = 160f;
        private float _collisionGracePeriod = 0.1f;

        // Final-leap parameters measured from replay bae83ecc (Sion netid 1073741857, leap at t=1108417):
        // a force-move with gravity 0, ~268 world-units at speed 605 (the leap's 0x64 WaypointGroupWithSpeed).
        // The leap fires ONLY on clean release/timeout — a wall/champion collision slams in place with NO
        // force-move (the collided-branch in OnUpdate FireCharges at the current position; replay-confirmed).
        private const float LeapDistance = 268f;
        private const float LeapSpeed = 605f;

        private float _chargeTime;

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
            _instantHit = false;
            _sion.StopMovement();
        }

        public void OnSpellChargeStart(Spell spell)
        {
            _buff = AddBuff("SionR", 8.5f, 1, spell, _sion, _sion);
            _chargeTime = 0f;
            // Reset the re-entry guard per cast — the script instance is reused across casts, so
            // without this every cast after the first would short-circuit in StopCharge.
            _chargeStopped = false;

            // Lock/steer the caster's camera for the charge (replay-verified, distance 900).
            LockCamera(_sion, true);

            _sion.IgnoreMoveOrders = true;
            spell.SpellData.CanMoveWhileChanneling = true;

            // Owner-only charge-state setup (replay-verified, 79a9129c: every SionR charge emits
            // S2C_UpdateSpellToggle(slot=3, ON) ~125ms after the start cast = at OnSpellChargeStart,
            // and OFF at release). Without it the owner client's R spell-state stays un-toggled while
            // the charge runs, so the HUD charge targeter (drawn while the spell is the active/running
            // charge) blinks on/off. Toggle OFF is done centrally in StopCharge. The paired
            // ChangeSlotSpellData_OwnerOnly(IconIndex=1) icon swap Riot also sends is cosmetic (R
            // button art) and omitted for now.
            spell.SetSpellToggle(true);

            Vector2 dir =
                Vector2.Normalize(new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z) -
                                  _sion.Position);
            _currentAngle = (float)Math.Atan2(dir.Y, dir.X);
            _targetAngle = _currentAngle;

            // Retail sends exactly ONE S2C_FaceDirection for the whole charge, ~one cast-windup
            // (SionR start-cast DesignerCastTime = 0.125s) after the start cast — which is exactly
            // when OnSpellChargeStart fires, matching the replay's ~130ms offset. Direction is the
            // cursor−caster delta, lerped over 0.0833s. Verified across 3 Sion replays / 25 charge
            // episodes: never re-sent during the hold. Facing during the run is then implicit from
            // the movement waypoints — do NOT re-emit facing per tick.
            _sion.FaceDirection(new Vector3(dir.X, 0f, dir.Y), false, 0.0833f);

            Vector2 checkPos = _sion.Position + dir * _sion.CollisionRadius;
            if (!IsWalkable(checkPos.X, checkPos.Y, 10f))
            {
                _instantHit = true;
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
                    _instantHit = true;
                    spell.FireCharge(_sion.Position);
                    StopCharge(true, false);
                }
            }

            // Kick off the forward run. Subsequent re-steering happens in OnSpellChargeUpdate at
            // the client's charge-update cadence; the engine's Move() walks this path between
            // updates while OnSpellChargeTick keeps the MoveTo order alive.
            if (!_instantHit)
            {
                SteerChargePath(dir);
            }
        }

        public void OnSpellChargeUpdate(Spell spell, Vector3 position, bool forceStop)
        {
            if (_instantHit || _chargeStopped || _sion.IsDead)
            {
                return;
            }

            Vector2 targetDir = Vector2.Normalize(new Vector2(position.X, position.Z) - _sion.Position);
            if (float.IsNaN(targetDir.X) || float.IsNaN(targetDir.Y))
            {
                return;
            }
            _targetAngle = (float)Math.Atan2(targetDir.Y, targetDir.X);

            // Re-steer the charge path HERE — once per client charge-update packet, which IS Riot's
            // WaypointGroup (0x61) cadence for a charge (replay: client-driven, irregular ~100-200ms).
            // This script previously re-broadcast the path on a fixed server-tick timer in
            // OnSpellChargeTick; that re-anchored the client every 100ms out of step with the client's
            // own input, and because the client anchors the charge indicator to the caster's position
            // (HudSpellLogic::DrawHudTargeterForSpell → Player->GetPosition()) the indicator flickered.
            // Driving the broadcast off the client's input cadence removes the beat/re-anchor. Heading
            // uses the gradually-turned _currentAngle (integrated in OnSpellChargeTick) so Sion still
            // cannot snap-turn.
            Vector2 newDir = new Vector2((float)Math.Cos(_currentAngle), (float)Math.Sin(_currentAngle));
            SteerChargePath(newDir);
        }

        // Points the charge run in `dir`: a short 2-waypoint path re-anchored at the caster's
        // current position (so wp0 advances monotonically, matching retail's 0x61 stream) and a
        // MoveTo order so Move() will actually walk it. isForced bypasses the channel CC gate.
        private void SteerChargePath(Vector2 dir)
        {
            _sion.UpdateMoveOrder(OrderType.MoveTo, false);
            Vector2 newPos = _sion.Position + dir * 500f;
            _sion.SetWaypoints(new List<Vector2> { _sion.Position, newPos }, true);
        }

        public void OnSpellChargeTick(Spell spell, float diff)
        {
            if (_instantHit) return;
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

            // A collision above may have ended the charge — don't touch movement afterwards.
            if (_chargeStopped) return;

            // Keep the MoveTo order alive (publish:false = direct field set, NO wire packet) so the
            // engine's Move() keeps walking Sion along the path last steered in OnSpellChargeUpdate.
            // The actual WaypointGroup broadcast happens on the client charge-update (Riot cadence),
            // NOT per server tick. ObjAIBase.Move() only advances Position under a "moving" order, so
            // this also overrides the AttackTo->CastSpell order rewrite StartChanneling applies at
            // channel start (otherwise Move() early-returns and the server never walks the path).
            _sion.UpdateMoveOrder(OrderType.MoveTo, false);
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

            // Clear the owner-only charge toggle set in OnSpellChargeStart (replay: OFF at release).
            _spell.SetSpellToggle(false);

            // Release the camera lock now the charge has ended (cancel / recast / timeout / collision).
            LockCamera(_sion, false);

            _sion.StopMovement();
            _sion.IgnoreMoveOrders = false;

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
                        ignoreTerrain: false,
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
                _sion.SetWaypoints(new List<Vector2> { _sion.Position, _sion.Position }, true);
                _sion.StopMovement();
                PlayAnimation(_sion, "Spell4", 0, 0, 1,
                    AnimationFlags.NoBlend | AnimationFlags.Junk5 | AnimationFlags.Junk7);
                _spell.CastInfo.InstanceVars.Set("hasLeaped", false);
                RemoveBuff(_buff);
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
                AddBuff("SionRSoundExplosionHitChampion", 0.25f, 1, _spell, _sion, _sion);
                RemoveBuff(_buff);
                OnHit();
            }
        }

        private void OnMoveEnd(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters.MovementName != "SionRLeap") return;
            PlayAnimation(_sion, "Spell4", 0, 0, 1,
                AnimationFlags.NoBlend | AnimationFlags.Junk5 | AnimationFlags.Junk7);
            _spell.CastInfo.InstanceVars.Set("hasLeaped", true);
            RemoveBuff(_buff);
            OnHit();
            ApiEventManager.OnMoveEnd.RemoveListener(this, _sion, OnMoveEnd);
        }

        private void OnHit()
        {
            if (_spell.CastInfo.InstanceVars.Get("hasLeaped", true))
            {
                var impactPos = _sion.Position + new Vector2(_sion.Direction.X, _sion.Direction.Z) * 200;
                AddBuff("SionRSoundExplosion", 0.25f, 1, _spell, _sion, _sion);
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
                AddBuff("SionRSoundExplosion", 0.25f, 1, _spell, _sion, _sion);
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