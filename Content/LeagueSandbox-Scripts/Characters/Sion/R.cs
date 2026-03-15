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
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class SionR : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            ChannelDuration = 8.0f,
            TriggersSpellCasts = true,
            AutoFaceDirection = true
        };

        private ObjAIBase _owner;
        private Spell _spell;
        private float _currentAngle;
        private float _targetAngle;
        private bool _isCharging;

        private float _unitHitboxRadius = 160f;
        private float _wallCheckRadius = 60f;
        private float _collisionGracePeriod = 0.25f;

        private StatsModifier _speedModifier;
        private float _currentBonusSpeed;
        private float _lastBonusSpeed;
        private float _maxBonusSpeed;
        private float _chargeTime;
        private float _waypointUpdateTimer;

        Buff b;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
        }

        public void OnSpellChannel(Spell spell)
        {
            b = AddBuff("SionR", 8f, 1, spell, _owner, _owner);
            _isCharging = true;
            _chargeTime = 0f;
            _waypointUpdateTimer = 0f;

            _owner.IgnoreMoveOrders = true;
            spell.SpellData.CanMoveWhileChanneling = true;
            _owner.SetStatus(StatusFlags.Ghosted, true);

            _maxBonusSpeed = Math.Max(0, 950f - _owner.GetMoveSpeed());
            _currentBonusSpeed = 0f;
            _lastBonusSpeed = 0f;

            _speedModifier = new StatsModifier();
            _owner.AddStatModifier(_speedModifier);

            Vector2 dir = Vector2.Normalize(new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z) - _owner.Position);
            _currentAngle = (float)Math.Atan2(dir.Y, dir.X);
            _targetAngle = _currentAngle;
        }

        public void OnSpellChannelUpdate(Spell spell, Vector3 position, bool forceStop)
        {
            Vector2 targetDir = Vector2.Normalize(new Vector2(position.X, position.Z) - _owner.Position);
            _targetAngle = (float)Math.Atan2(targetDir.Y, targetDir.X);
        }

        public void OnUpdate(float diff)
        {
            if (!_isCharging || _owner == null || _owner.IsDead) return;

            float deltaSeconds = diff / 1000f;
            _chargeTime += deltaSeconds;

            if (_currentBonusSpeed < _maxBonusSpeed)
            {
                _currentBonusSpeed += 250f * deltaSeconds;
                if (_currentBonusSpeed > _maxBonusSpeed) _currentBonusSpeed = _maxBonusSpeed;

                if (_currentBonusSpeed - _lastBonusSpeed > 25f || _currentBonusSpeed == _maxBonusSpeed)
                {
                    if (_speedModifier != null)
                    {
                        _owner.RemoveStatModifier(_speedModifier);
                    }

                    _speedModifier.MoveSpeed.FlatBonus = _currentBonusSpeed;
                    _owner.AddStatModifier(_speedModifier);
                    _lastBonusSpeed = _currentBonusSpeed;
                }
            }

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
                bool collided = false;
                Vector2 checkPos = _owner.Position + newDir * _wallCheckRadius;
                if (!IsWalkable(checkPos.X, checkPos.Y, 10f))
                {
                    collided = true;
                }
                else
                {
                    var nearbyUnits = GetUnitsInRange(_owner.Position, _unitHitboxRadius + 100f, true);
                    foreach (var unit in nearbyUnits)
                    {
                        if (unit is Champion && unit.Team == _owner.Team) continue;

                        if (unit is Champion || unit is BaseTurret || unit is Inhibitor)
                        {
                            float dist = Vector2.Distance(_owner.Position, unit.Position);
                            if (dist <= _unitHitboxRadius + unit.CollisionRadius)
                            {
                                collided = true;
                                break;
                            }
                        }
                    }
                }

                if (collided)
                {
                    StopCharge(true);
                    return;
                }
            }

            _waypointUpdateTimer -= diff;
            if (_waypointUpdateTimer <= 0f)
            {
                Vector2 newPos = _owner.Position + newDir * 500f;
                _owner.SetWaypoints(new List<Vector2> { _owner.Position, newPos }, true);
                _waypointUpdateTimer = 100f; 
            }
        }

        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            StopCharge(false);
        }

        public void OnSpellPostChannel(Spell spell)
        {
            StopCharge(false);
        }

        private void StopCharge(bool hitSomething)
        {
            if (!_isCharging) return;
            _isCharging = false;

            if (_owner != null)
            {
                _owner.IgnoreMoveOrders = false;
                _owner.SetStatus(StatusFlags.Ghosted, false);
                _owner.StopMovement();

                if (_speedModifier != null)
                {
                    _owner.RemoveStatModifier(_speedModifier);
                    _speedModifier = null;
                }

                if (b != null)
                {
                    b.SetToExpired();
                    b.DeactivateBuff();
                }

                if (hitSomething)
                {
                    if (_owner.ChannelSpell == _spell)
                    {
                        _owner.StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.LostTarget);
                    }
                    PlayAnimation(_owner, "Spell4_Hit");
                    OnHit();
                }
                else if (!_owner.IsDead)
                {
                    Vector2 dir2D = new Vector2((float)Math.Cos(_currentAngle), (float)Math.Sin(_currentAngle));

                    _owner.RegisterTimer(new GameScriptTimer(0.10f, () =>
                    {
                        if (!_owner.IsDead)
                        {
                            _owner.DashToLocation(_owner.Position + dir2D * 300, 545, "Spell4_Stop", 0.0f, false);//0.55 
                            _owner.RegisterTimer(new GameScriptTimer(0.55f, () =>
                            {
                                OnHit();
                            }));
                        }
                    }));
                }
            }
        }
        public void OnHit()
        {
            AddParticle(_owner, default, "sion_base_r_explosion.troy", _owner.Position + new Vector2(_owner.Direction.X, _owner.Direction.Z) * 200);
            // dmg, knockups, stun etc.
        }
    }
}