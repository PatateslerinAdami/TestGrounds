using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.SpellNS.Missile
{
    public class SpellChainMissile : SpellMissile
    {
        public override MissileType Type { get; protected set; } = MissileType.Chained;

        /// <summary>
        /// Number of objects this projectile has hit since it was created.
        /// </summary>
        public List<GameObject> ObjectsHit { get; }
        /// <summary>
        /// Total number of times this missile has hit any units.
        /// </summary>
        /// TODO: Verify if we want this to be an array for different MaximumHit counts for: CanHitCaster, CanHitEnemies, CanHitFriends, CanHitSameTarget, and CanHitSameTargetConsecutively.
        public int HitCount { get; protected set; }
        /// <summary>
        /// Parameters for this chain missile, refer to MissileParameters.
        /// </summary>
        public MissileParameters Parameters { get; protected set; }
        private float _bounceRadius;
        SpellDataFlags OverrideFlags { get; }

        private bool _isPendingDestroy = false;
        private float _destroyTimer = 0.0f;
        private const float DESTROY_DELAY = 50.0f;
        public SpellChainMissile(
            Game game,
            int collisionRadius,
            Spell originSpell,
            CastInfo castInfo,
            MissileParameters parameters,
            float moveSpeed,
            SpellDataFlags overrideFlags = 0, // TODO: Find a use for these
            uint netId = 0,
            bool serverOnly = false
        ) : base(game, collisionRadius, originSpell, castInfo, moveSpeed, overrideFlags, netId, serverOnly)
        {
            ObjectsHit = new List<GameObject>();
            HitCount = 0;
            Parameters = parameters;
            _bounceRadius = originSpell.SpellData.BounceRadius;
            OverrideFlags = overrideFlags;
        }

        public override void Update(float diff)
        {
            if (_isPendingDestroy)
            {
                _destroyTimer -= diff;
                if (_destroyTimer <= 0)
                {
                    base.SetToRemove();
                }
                return;
            }

            base.Update(diff);

            // TODO: Verify if we can move this into CheckFlagsForUnit instead of checking every Update.
            if (HitCount >= Parameters.MaximumHits)
            {
                SetToRemove();
            }
        }

        public override void CheckFlagsForUnit(AttackableUnit unit)
        {
            if (_isPendingDestroy)
            {
                return;
            }
            if (!IsValidTarget(unit))
            {
                BounceToNextTarget();
                SetToRemove();
                return;
            }

            ObjectsHit.Add(unit);
            HitCount++;

            // Targeted Spell (including auto attack spells)
            if (SpellOrigin != null)
            {
                SpellOrigin.ApplyEffects(TargetUnit, this);
            }

            if (CastInfo.Owner is ObjAIBase ai && SpellOrigin.CastInfo.IsAutoAttack)
            {
                ai.AutoAttackHit(TargetUnit);
            }

            BounceToNextTarget();
            SetToRemove();
        }
        private void BounceToNextTarget()
        {
            if (HitCount >= Parameters.MaximumHits)
            {
                return;
            }

            AttackableUnit nextTarget = null;
            float shortestDist = float.MaxValue;

            var units = _game.ObjectManager.GetUnitsInRange(Position, _bounceRadius, true);

            foreach (var unit in units)
            {
                if (IsValidTarget(unit))
                {
                    float dist = Vector2.DistanceSquared(Position, unit.Position);
                    if (dist < shortestDist)
                    {
                        shortestDist = dist;
                        nextTarget = unit;
                    }
                }
            }

            if (nextTarget != null)
            {
                var newCastInfo = CastInfo.Clone();

                newCastInfo.MissileNetID = _game.NetworkIdManager.GetNewNetId();

                newCastInfo.SpellCastLaunchPosition = TargetUnit != null ? TargetUnit.GetPosition3D() : GetPosition3D();
                newCastInfo.IsOverrideCastPosition = true;
                newCastInfo.SpellChainOwnerNetID = TargetUnit != null ? TargetUnit.NetId : (CastInfo.Owner != null ? CastInfo.Owner.NetId : 0);

                newCastInfo.TargetPosition = nextTarget.GetPosition3D();
                newCastInfo.TargetPositionEnd = nextTarget.GetPosition3D();

                newCastInfo.Targets = new List<CastTarget> { new CastTarget(nextTarget, HitResult.HIT_Normal) };

                string nextSpellName = Parameters.BounceSpellName;
                float nextSpeed = _moveSpeed;
                float nextRadius = _bounceRadius;

                if (!string.IsNullOrEmpty(nextSpellName))
                {
                    newCastInfo.SpellHash = (uint)GameServerCore.Content.HashFunctions.HashString(nextSpellName);

                    // Try to get data for the bounce spell to update speed/radius
                    try
                    {
                        var bounceData = _game.Config.ContentManager.GetSpellData(nextSpellName);
                        nextSpeed = bounceData.MissileSpeed;
                        nextRadius = bounceData.BounceRadius;
                    }
                    catch
                    {
                        // Fallback: keep current speed/radius if data not found
                    }
                }

                var newMissile = new SpellChainMissile(
                    _game,
                    (int)CollisionRadius,
                    SpellOrigin,
                    newCastInfo,
                    Parameters,
                    nextSpeed,
                    OverrideFlags,
                    newCastInfo.MissileNetID,
                    IsServerOnly
                );

                newMissile.SetChainState(ObjectsHit, HitCount);
                newMissile.SetBounceStats(nextRadius);

                _game.ObjectManager.AddObject(newMissile);

                API.ApiEventManager.OnLaunchMissile.Publish(SpellOrigin, newMissile);
            }
        }
        /// <summary>
        /// Transfers the history of hit objects and the current hit count to the new missile.
        /// </summary>
        public void SetChainState(List<GameObject> objectsHit, int hitCount)
        {
            ObjectsHit.AddRange(objectsHit);
            HitCount = hitCount;
        }
        /// <summary>
        /// Updates the bounce radius for this missile (used when switching to BounceSpellName data).
        /// </summary>
        public void SetBounceStats(float radius)
        {
            _bounceRadius = radius;
        }

        protected bool IsValidTarget(AttackableUnit unit, bool checkOnly = false)
        {
            bool valid = SpellOrigin.SpellData.IsValidTarget(CastInfo.Owner, unit);
            bool hit = ObjectsHit.Contains(unit);

            if (hit)
            {
                // We can't hit this unit because we've hit it already.
                valid = false;

                // We can consecutively hit this same unit until we run out of bounces.
                if (Parameters.CanHitSameTarget && Parameters.CanHitSameTargetConsecutively)
                {
                    valid = true;
                }
                // We can hit it again after we bounce once.
                else if (Parameters.CanHitSameTarget && !checkOnly)
                {
                    ObjectsHit.Remove(unit);
                }
            }
            // Otherwise, we can hit this unit because we haven't hit it yet.

            return valid;
        }
        public override void SetToRemove()
        {
            // Instead of removing immediately, delay destruction slightly
            // so the client has time to process the hit.
            if (!_isPendingDestroy && !IsToRemove())
            {
                _isPendingDestroy = true;
                _destroyTimer = DESTROY_DELAY;
            }
        }

    }
}
