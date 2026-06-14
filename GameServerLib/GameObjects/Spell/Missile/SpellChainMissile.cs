using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content;
using System;
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
        /// Total number of times this missile chain has hit any units, carried across
        /// segments via SetChainState. Mirrors S4's single mi_numTargetsAlreadyHit counter
        /// checked against the per-level mi_MaximumHits[6] budget (SpellChainMissile.cpp:498)
        /// — the CanHit* flags are target FILTERS, not separate budgets.
        /// </summary>
        private int _chainHitCount;
        public override int HitCount => _chainHitCount;
        /// <summary>
        /// Parameters for this chain missile, refer to MissileParameters.
        /// </summary>
        public MissileParameters Parameters { get; protected set; }
        private float _bounceRadius;

        // Shared across segments: each bounce is a NEW missile instance; per-instance
        // seeding is wasteful and the game loop is single-threaded.
        private static readonly Random _random = new Random();
        public SpellChainMissile(
            Game game,
            int collisionRadius,
            Spell originSpell,
            CastInfo castInfo,
            MissileParameters parameters,
            float moveSpeed,
            uint netId = 0,
            bool serverOnly = false
        ) : base(game, collisionRadius, originSpell, castInfo, moveSpeed, netId, serverOnly)
        {
            _chainHitCount = 0;
            Parameters = parameters;
            _bounceRadius = originSpell.SpellData.BounceRadius;
        }

        public override void Update(float diff)
        {
            if (IsToRemove())
            {
                return;
            }

            // CastType-2 spells are engine-native chain missiles CLIENT-side
            // (TryFireSpellMissile -> CreateChainMissile waits for its target list via
            // S2C_ChainMissileSync). Riot streams the sync ONCE PER SERVER TICK from
            // launch THROUGH the hit tick for EVERY such missile — Sivir W: the
            // empowered AA missile AND each bounce hop (replays ccca6cd9/90a21985 show
            // ~30ms gaps = the replay era's 30Hz tickrate; at 60Hz tournament rate this
            // scales with the tick, which is the intended semantic — Update runs exactly
            // once per tick). Emitted BEFORE the move so the dying missile still sends
            // its final sync on the tick it hits (replay: one last CMS of the old
            // missile shares the hit timestamp with the new segment's spawn). Data-
            // driven from the read-only JSON; CastType-1 chains never emit.
            if (SpellOrigin?.SpellData.CastType == 2)
            {
                _game.PacketNotifier.NotifyS2C_ChainMissileSync(this);
            }

            // Mid-flight target death (e.g. the Q target is killed by another of Katarina's spells before
            // the dagger lands): Riot does NOT destroy the chain missile — it bounces on from the dying
            // target, exactly like reaching an already-invalid target on arrival. Replay-verified: 0
            // DestroyClientMissile across 367 Katarina Q-family missiles in a full match. Base SpellMissile
            // would instead SetToRemove WITHOUT bouncing AND send a destroy, so intercept it here: suppress
            // the destroy (on-arrival-style removal) and continue the chain. The dead target is NOT counted
            // as a hit (mirrors CheckFlagsForUnit's invalid-target branch).
            if (!IsToRemove() && HasTarget()
                && (TargetUnit.IsDead || !TargetUnit.Status.HasFlag(StatusFlags.Targetable)))
            {
                SuppressDestroyNotify = true;
                BounceToNextTarget();
                SetToRemove();
                return;
            }

            base.Update(diff);

            // No hit-cap check here: S4 caps BOUNCING (in the bounce advance), never the
            // missile's existence — each segment removes itself after its single hit in
            // CheckFlagsForUnit, and BounceToNextTarget refuses to spawn past the budget.
        }

        public override void CheckFlagsForUnit(AttackableUnit unit)
        {
            if (IsToRemove())
            {
                return;
            }
            // Both exits below are ON-ARRIVAL removals: the client's missile reached the
            // same point and terminates itself — Riot sends NO destroy packet for these
            // (replay: 0 destroys across 2000+ chain segments of all five chain spells).
            // Mid-flight kills (target died/untargetable, handled in SpellMissile.Update)
            // keep the destroy notify.
            SuppressDestroyNotify = true;
            if (!IsValidTarget(unit))
            {
                BounceToNextTarget();
                SetToRemove();
                return;
            }

            ObjectsHit.Add(unit);
            _chainHitCount++;

            // Targeted Spell (including auto attack spells)
            if (SpellOrigin != null)
            {
                SpellOrigin.ApplyEffects(TargetUnit, this);
            }

            // Per-hit impact FX from the spell JSON — Riot sends an FX_Create_Group on EVERY chain hit
            // (replay: katarina_bouncingBlades_tar / DarkWind_tar per bounce). The engine's ApplyEffects
            // HitEffect path only fires for EMPTY-script spells (Spell.cs gates on HasEmptyScript), so
            // scripted chains (Katarina Q, Fiddle E, …) showed no hit FX — and only on the initial hit if
            // the script spawned it manually. Spawn it here so the initial hit AND every bounce render it,
            // read straight from the JSON (with HitBone when set). AA-bound chains (Sivir W) keep the
            // client-automatic AA hit FX path instead, and empty-script chains are already covered by
            // ApplyEffects — gate both out to avoid a double spawn.
            if (TargetUnit != null
                && !SpellOrigin.CastInfo.IsAutoAttack
                && !SpellOrigin.HasEmptyScript
                && SpellOrigin.SpellData.HaveHitEffect
                && !string.IsNullOrEmpty(SpellOrigin.SpellData.HitEffectName))
            {
                if (SpellOrigin.SpellData.HaveHitBone)
                {
                    API.ApiFunctionManager.AddParticleTarget(CastInfo.Owner, null, SpellOrigin.SpellData.HitEffectName, TargetUnit, targetBone: SpellOrigin.SpellData.HitBoneName, lifetime: 1.0f);
                }
                else
                {
                    API.ApiFunctionManager.AddParticleTarget(CastInfo.Owner, null, SpellOrigin.SpellData.HitEffectName, TargetUnit, lifetime: 1.0f);
                }
            }

            // Attack-bound chains (Sivir W): only the FIRST hit is the real auto attack
            // (full AD + crit + on-hit pipeline). Bounce damage is script-side, mirroring
            // Riot's per-spell Lua (BBApplyDamage with PercentOfAttack — bounces deal a
            // reduced AD fraction defined by the spell, e.g. SivirW Effect3 = 50-70%).
            // HitCount was already incremented above, so the first hit reads 1.
            if (CastInfo.Owner is ObjAIBase ai && SpellOrigin.CastInfo.IsAutoAttack)
            {
                if (HitCount == 1)
                {
                    ai.AutoAttackHit(TargetUnit);
                }
                else
                {
                    // Bounce hits of attack chains have no damage channel otherwise:
                    // ApplyEffects deliberately skips OnSpellHit for AA casts (it only
                    // publishes OnBeingHit) and AutoAttackHit is first-hit-only. Publish
                    // OnSpellHit here so the spell script receives the bounce and applies
                    // its reduced damage.
                    API.ApiEventManager.OnSpellHit.Publish(SpellOrigin, (TargetUnit, (SpellMissile)this, (Sector.SpellSector)null));
                }
            }

            BounceToNextTarget();
            SetToRemove();
        }
        private void BounceToNextTarget()
        {
            // Per-level hit budget (S4: mi_MaximumHits[SpellLevel]). With a budget of 0
            // this is 0 >= 0 -> never bounce, i.e. a plain single-target missile —
            // matching S4 chain semantics (NOT the line-missile "0 = uncapped" rule).
            if (HitCount >= Parameters.GetMaximumHits(CastInfo.SpellLevel))
            {
                return;
            }

            // Alternating ally/enemy chain (Nami W): BOTH bounce names set -> the next
            // segment targets the OPPOSITE allegiance of the unit this missile just hit and
            // fully switches to the corresponding spell (script, parameters, data).
            // Exactly one name set (either field) = legacy single-pool chain.
            bool alternating = !string.IsNullOrEmpty(Parameters.BounceSpellNameAlly)
                            && !string.IsNullOrEmpty(Parameters.BounceSpellNameEnemy);
            bool nextPoolIsAlly = false;
            Spell nextSpell = null;
            if (alternating)
            {
                bool currentTargetIsAlly = TargetUnit != null && TargetUnit.Team == CastInfo.Owner.Team;
                nextPoolIsAlly = !currentTargetIsAlly;
                var alternatingName = nextPoolIsAlly ? Parameters.BounceSpellNameAlly : Parameters.BounceSpellNameEnemy;
                nextSpell = CastInfo.Owner.GetSpell(alternatingName);
                if (nextSpell == null)
                {
                    // Counterpart spell not registered — chain ends here.
                    return;
                }
            }

            float searchRadius = _bounceRadius;
            if (alternating && nextSpell.SpellData.BounceRadius > 0f)
            {
                searchRadius = nextSpell.SpellData.BounceRadius;
            }

            // Candidate pool for the next-target pick; the pick rule is per-spell
            // (Parameters.BounceSelection — see its docs for the replay evidence).
            var candidates = new List<AttackableUnit>();

            // Search around the HIT TARGET's position, not the missile's. On arrival these coincide, but on
            // a MID-FLIGHT target death the missile is still between caster and target while the other
            // enemies cluster around the (now-dead) target — searching the missile's mid-flight position
            // would find nothing and silently end the chain. The next segment also launches from the
            // target's position (below), so this keeps search + launch consistent.
            var searchCenter = TargetUnit != null ? TargetUnit.Position : Position;
            var units = _game.ObjectManager.GetUnitsInRange(searchCenter, searchRadius, true);

            foreach (var unit in units)
            {
                bool valid;
                if (alternating)
                {
                    // Pool filter by allegiance + the NEXT spell's own target validity.
                    // Alternating chains never revisit a unit (Nami W semantics) — the
                    // CanHitSameTarget knobs only apply to the legacy single-pool path.
                    // BounceAffectsOverride narrows the JSON flags (replay-verified:
                    // Nami W bounces are champion-only despite AffectMinions in the JSON).
                    var nextBounceFlags = (nextSpell.Script?.ScriptMetadata?.MissileParameters ?? Parameters).BounceAffectsOverride;
                    valid = (unit.Team == CastInfo.Owner.Team) == nextPoolIsAlly
                        && !ObjectsHit.Contains(unit)
                        && nextSpell.SpellData.IsValidTarget(CastInfo.Owner, unit, nextBounceFlags);
                }
                else
                {
                    valid = IsValidTarget(unit);
                }

                if (valid)
                {
                    candidates.Add(unit);
                }
            }

            AttackableUnit nextTarget = null;
            if (candidates.Count > 0)
            {
                if (Parameters.BounceSelection == BounceSelection.Nearest)
                {
                    float shortestDist = float.MaxValue;
                    foreach (var candidate in candidates)
                    {
                        float dist = Vector2.DistanceSquared(Position, candidate.Position);
                        if (dist < shortestDist)
                        {
                            shortestDist = dist;
                            nextTarget = candidate;
                        }
                    }
                }
                else
                {
                    nextTarget = candidates[_random.Next(candidates.Count)];
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

                // Attack-bound chains roll crit PER BOUNCE, INDEPENDENTLY, at segment
                // creation — the result is baked into this segment's wire CastInfo
                // (replay: bounce MISREPs carry their own HitResult 0/1; time-stratified
                // analysis shows the rate equals the CURRENT crit chance regardless of
                // the triggering attack's result). Scripts must READ this byte for the
                // bounce damage instead of rolling again — one roll, wire/visuals/damage
                // always consistent.
                var bounceHitResult = HitResult.HIT_Normal;
                if (SpellOrigin.CastInfo.IsAutoAttack && CastInfo.Owner is ObjAIBase owner
                    && _random.NextDouble() < owner.Stats.CriticalChance.Total)
                {
                    bounceHitResult = HitResult.HIT_Critical;
                }
                newCastInfo.Targets = new List<CastTarget> { new CastTarget(nextTarget, bounceHitResult) };

                string nextSpellName = !string.IsNullOrEmpty(Parameters.BounceSpellNameEnemy)
                    ? Parameters.BounceSpellNameEnemy
                    : Parameters.BounceSpellNameAlly;
                float nextSpeed = _moveSpeed;
                float nextRadius = _bounceRadius;
                var nextOrigin = SpellOrigin;
                var nextParameters = Parameters;

                if (alternating)
                {
                    // Full spell switch: the new segment runs under the next spell's script
                    // (its OnSpellHit does the heal/damage), parameters (carrying the
                    // cross-referenced bounce names) and data.
                    nextOrigin = nextSpell;
                    nextParameters = nextSpell.Script?.ScriptMetadata?.MissileParameters ?? Parameters;
                    nextSpeed = nextSpell.SpellData.MissileSpeed;
                    if (nextSpell.SpellData.BounceRadius > 0f)
                    {
                        nextRadius = nextSpell.SpellData.BounceRadius;
                    }
                    newCastInfo.SpellHash = (uint)nextSpell.GetId();
                }
                else if (!string.IsNullOrEmpty(nextSpellName))
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
                    nextOrigin,
                    newCastInfo,
                    nextParameters,
                    nextSpeed,
                    newCastInfo.MissileNetID,
                    IsServerOnly
                );

                newMissile.SetChainState(ObjectsHit, HitCount);
                newMissile.SetBounceStats(nextRadius);
                // No CastSpellAns / Basic_Attack_Pos accompanies a bounce. The visibility
                // path needs to send the full MissileReplication so the client learns about
                // this previously-unannounced missile (replay-verified: KatarinaQMis hash
                // 156298371 has 340 MissileReplications across the match, none with a
                // matching CastSpellAns).
                newMissile.HasClientCastInfo = false;

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
            _chainHitCount = hitCount;
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
            bool valid = SpellOrigin.SpellData.IsValidTarget(CastInfo.Owner, unit, Parameters.BounceAffectsOverride);

            // Riot's chain-specific CanHit* filters (S4 mi_CanHitSelf/-Friends/-Enemies,
            // defaults enemies-only) apply ON TOP of the SpellData target flags. Legacy
            // single-pool chains only — alternating chains (both bounce names set) filter
            // by pool allegiance in BounceToNextTarget instead, like the CanHitSameTarget
            // knobs.
            bool alternating = !string.IsNullOrEmpty(Parameters.BounceSpellNameAlly)
                            && !string.IsNullOrEmpty(Parameters.BounceSpellNameEnemy);
            if (!alternating)
            {
                if (unit == CastInfo.Owner)
                {
                    // CanHitCaster OVERRIDES the allegiance gates entirely instead of
                    // ANDing: Riot ran CanHitCaster=1 with enemies-only parent flags
                    // (S1 SpellFlux Lua; the bounce-missile JSON carries the AlwaysSelf
                    // marker). A plain AND could never pass — and plain AffectFriends
                    // would be too broad (Spell Flux bounces to Ryze, NOT other allies).
                    valid = Parameters.CanHitCaster && !unit.IsDead;
                }
                else if (unit.Team == CastInfo.Owner.Team)
                {
                    valid &= Parameters.CanHitFriends;
                }
                else
                {
                    valid &= Parameters.CanHitEnemies;
                }
            }

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
    }
}
