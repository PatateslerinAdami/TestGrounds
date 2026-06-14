using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class VelkozR : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            // ChargeDuration is resolved at runtime by GetEffectiveChannelDuration from
            // VelkozR.json ChannelDuration = 2.6 (no SpellTargeter block in this JSON).
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            AutoFaceDirection = false
        };

        private ObjAIBase _owner;
        private Marker _laserTarget;
        private Vector2 _end;

        private Particle _eye;
        private Particle _lens;
        private Particle _beam;
        private Particle _beamEnd;
        private Particle _lensbeamC;
        private Particle _lensbeamL;
        private Particle _lensbeamR;

        private PeriodicTicker _damageTicker;

        // Beam line dimensions (units). Range matches SpellData.CastRange (1550).
        // Half-width 80 is a reasonable approximation of the visible beam thickness;
        // tune against in-game feel if needed.
        private const float BeamRange = 1550f;
        private const float BeamHalfWidth = 80f;

        // Marker glide speed (MoveMarker Speed field). Replay a6db3774: constant 1033.3333 u/s.
        private const float BeamGlideSpeed = 1033.3333f;

        // Gradual-sweep cap: the MoveMarker GOAL advances at most this far per client charge-update,
        // so the beam turns gradually (wiki: "direction updates gradually — moving the cursor from
        // one side to the other will not make him rotate instantly"). Replay a6db3774: goal step is
        // capped at ~104u PER PACKET regardless of cadence (104u steps occur even on 44ms intervals,
        // so it's a fixed per-update step, not a u/s rate) = BeamGlideSpeed × ChargeUpdateInterval(0.1).
        private const float BeamSweepStepPerUpdate = BeamGlideSpeed * 0.1f; // ~103.3u

        // Capped sweep goal (advanced toward the raw cursor by <= BeamSweepStepPerUpdate each update).
        private Vector2 _steerGoal;

        // Damage ticks every 250ms; Vel'Koz R (4.x) totals 10 ticks over the 2.5s
        // active-beam window (post-windup), for 500/700/900 (+0.8 AP) total damage.
        // Per-tick = 50/70/90 (+0.08 AP). Three ultimate ranks only.
        private const float DamageTickIntervalMs = 250f;
        private static readonly float[] BaseDamagePerTick = { 50f, 70f, 90f };
        private const float ApRatioPerTick = 0.08f;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            // The marker is spawned per-cast in OnSpellChargeStart (replay-faithful), not
            // here at level-up time. Spawning at activation worked server-side but the
            // client only learned about the marker at activation position; subsequent
            // SetPosition/MoveTo packets then tried to interpolate from the stale level-up
            // position, dragging the beam visual through wrong space.
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _end = end;
            FaceDirection(end, owner,true);
            AddBuff("VelkozR", 2.85f, 1, spell, _owner, _owner);
        }

        public void OnSpellChargeStart(Spell spell)
        {
            _damageTicker.Reset();

            // Marker steering (MoveTo + FaceDirection) runs per client charge-update in
            // OnSpellChargeUpdate (Riot-faithful cadence). OnSpellChargeStart only spawns the
            // marker; the caster is already aimed by OnSpellPreCast's FaceDirection call.

            // Spawn the marker at the press-time click target (captured by OnSpellPreCast
            // into _end). Riot's wire-side target was the cursor direction projected to
            // CastRange (1550u), but in our pipeline the OnSpellPreCast `end` parameter is
            // the resolved press target — using it directly avoids snap-on-spawn issues
            // when the direction-derived endpoint would have fallen inside terrain that
            // the navgrid steers GameObject spawns away from.
            // Marker Y is auto-resolved by GetHeight() to terrain at marker's own XZ —
            // no need to pass it. Previously we passed _owner.GetHeight() which set the
            // marker's spawn Y to caster's terrain, causing a Y mismatch when caster
            // and click target are at different terrain heights.
            _laserTarget = AddMarker(_end, team: _owner.Team);
            _steerGoal = _laserTarget.Position; // sweep goal starts at the spawn endpoint

            // Movement/cast/attack lock is now applied by the engine channel pipeline from
            // SpellData (Spell.Channel: CanCast/CanAttack always off during a channel/charge,
            // CanMove off because VelkozR.json CanMoveWhileChanneling=0), and released on every
            // channel-end path. No manual SetStatus/StopMovement needed here, and the charge
            // recast still works (it routes through UpdateCharge, not the CanCast-gated cast path).

            _eye = AddParticleTarget(_owner, _owner, "velkoz_base_r_beam_eye.troy", _owner, lifetime: 2.6f, bone: "Buffbone_Cstm_EyeBall", targetBone: "Buffbone_Cstm_EyeBallTarget");
            _lens = AddParticle(_owner, _owner, "velkoz_base_r_lens.troy", _owner.Position, lifetime: 2.6f, bone: "Buffbone_Cstm_EyeBallTarget");

            // Replay wire for _R_beam needs two distinct world coords: Pos.xz = caster (so
            // the .troy resolves the start to the EyeBallTarget bone) and Target.xz = marker
            // endpoint. AddParticleTarget(bindObj+target) would collapse both onto the marker
            // (rendering only a point at the endpoint), so use the 2-arg overload to keep them
            // distinct, then patch TargetNetID via override.
            _beam = AddParticleTarget(_owner, _owner, "velkoz_base_r_beam.troy", _laserTarget, lifetime: 2.6f, bone: "Buffbone_Cstm_EyeBallTarget", targetBone: "root");
            // Replay wire for Velkoz_Base_R_Beam_End (caster-POV replay):
            //   BindNetID=marker, TargetNetID=0, CasterNetID=Vel'Koz, KeywordNetID=0
            //   Pos.xz = marker pos,                Pos.y = marker terrain (~51.43)
            //   Target.xz = (-3679,-3706) sentinel, Target.y = 0
            //   Owner.xz = marker pos,              Owner.y = marker terrain (~51.43)
            //   Bone=0, TargetBone=0, Flags=BindDirection (0x20)
            //
            // ownerPos auto-resolves to marker.GetPosition3D() via the smart default in
            // ConstructFXCreateGroupPacket: TargetObject is null (2-arg AddParticle), so it
            // falls through to BindObject = marker. No script-side override needed.
            // keywordNetIDOverride MUST stay as ctor param — set after construction wouldn't
            // affect the FX_Create_Group bytes since the packet is built synchronously inside
            // Particle.ctor → AddObject and cached in the per-recipient batch.
            var offMapTarget = new Vector2(-367f, -421f); // int16-encodes to (-3679,-3706)
            _beamEnd = AddParticle(_owner, _laserTarget, "Velkoz_Base_R_Beam_End.troy", offMapTarget,
                lifetime: 2.6f, followGroundTilt: true);

            _lensbeamC = AddParticleTarget(_owner, _owner, "velkoz_base_r_lensbeam.troy", _owner, lifetime: 2.6f, bone: "C_Buffbone_Cstm_Tenticle", targetBone: "Buffbone_Cstm_EyeBallTarget");
            _lensbeamL = AddParticleTarget(_owner, _owner, "velkoz_base_r_lensbeam.troy", _owner, lifetime: 2.6f, bone: "L_Buffbone_Cstm_Tenticle", targetBone: "Buffbone_Cstm_EyeBallTarget");
            _lensbeamR = AddParticleTarget(_owner, _owner, "velkoz_base_r_lensbeam.troy", _owner, lifetime: 2.6f, bone: "R_Buffbone_Cstm_Tenticle", targetBone: "Buffbone_Cstm_EyeBallTarget");
        }

        public void OnSpellChargeUpdate(Spell spell, Vector3 position, bool forceStop)
        {
            // Marker steering runs HERE — once per client charge-update packet, which IS Riot's
            // MoveMarker cadence (the client heartbeats SpellChargeUpdateReq ~every
            // ChargeUpdateInterval; replay a6db3774: ~100ms avg, irregular 56-139ms). Driving it
            // per packet (not a regular server tick) matches that cadence and avoids the tick
            // stutter. Two replay-verified pieces make it look like Riot:
            //   1) GRADUAL sweep: the goal advances toward the cursor by at most
            //      BeamSweepStepPerUpdate (~104u/packet) — so a fast cursor flick turns the beam
            //      gradually, not instantly (wiki: "direction updates gradually").
            //   2) The marker glides to that goal at Speed=1033, FaceGoal=true.
            if (_laserTarget == null)
            {
                return;
            }
            Vector2 dir = Vector2.Normalize(new Vector2(position.X, position.Z) - _owner.Position);
            if (float.IsNaN(dir.X) || float.IsNaN(dir.Y))
            {
                return;
            }
            Vector2 desired = _owner.Position + dir * BeamRange;
            Vector2 toDesired = desired - _steerGoal;
            float dd = toDesired.Length();
            _steerGoal = (dd > BeamSweepStepPerUpdate)
                ? _steerGoal + toDesired / dd * BeamSweepStepPerUpdate
                : desired;
            // Re-project onto the full-range arc so the endpoint stays at 1550 (a capped linear
            // step would otherwise pull the goal slightly inward).
            Vector2 fromOwner = _steerGoal - _owner.Position;
            if (fromOwner.LengthSquared() > float.Epsilon)
            {
                _steerGoal = _owner.Position + Vector2.Normalize(fromOwner) * BeamRange;
            }
            _laserTarget.MoveTo(_steerGoal, BeamGlideSpeed);

            // Face the CAPPED sweep direction (not the raw cursor), lerped over 0.1s (replay:
            // S2C_FaceDirection DoLerp=1, LerpTime=0.100) — so Velkoz turns gradually with the beam.
            Vector2 faceDir = Vector2.Normalize(_steerGoal - _owner.Position);
            _owner.FaceDirection(new Vector3(faceDir.X, 0, faceDir.Y), isInstant: false, turnTime: 0.1f);
        }

        public void OnSpellChargeTick(Spell spell, float diff)
        {
            // Server-authoritative DAMAGE only (steering moved to OnSpellChargeUpdate so it
            // tracks the client's input cadence). Laser is instantaneous — first tick fires
            // immediately, then every DamageTickIntervalMs. Damage follows the gliding marker
            // position (see ApplyBeamDamage), independent of client packet timing.
            var ticks = _damageTicker.ConsumeTicks(diff, DamageTickIntervalMs, fireImmediately: true);
            for (var i = 0; i < ticks; i++)
            {
                ApplyBeamDamage(spell);
            }
        }

        private void ApplyBeamDamage(Spell spell)
        {
            // Beam follows the GLIDING marker position (the visible beam), not the raw input —
            // so damage lands where the laser actually is mid-steer.
            Vector2 dir = _laserTarget.Position - _owner.Position;
            dir = dir.LengthSquared() > float.Epsilon ? Vector2.Normalize(dir) : new Vector2(1, 0);
            Vector2 beamEnd = _owner.Position + dir * BeamRange;

            var hits = GetUnitsInPolygon(_owner, _owner.Position, beamEnd - _owner.Position,
                BeamHalfWidth * 2f, BeamRange,
                new[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-0.5f, 1f) },
                true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectMinions
                | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes);

            int idx = Math.Clamp(spell.CastInfo.SpellLevel - 1, 0, BaseDamagePerTick.Length - 1);
            float damage = BaseDamagePerTick[idx] + _owner.Stats.AbilityPower.Total * ApRatioPerTick;

            // Replay-verified wire: bones empty, BindNetID=TargetNetID=hit-unit,
            // Flags = GivenDirection | BindDirection. Orientation = enemy→caster
            // direction (the direction the impact came FROM, so spark animation
            // orients back toward the beam source). Computed per-unit since each
            // enemy has its own line-of-impact.
            foreach (var unit in hits)
            {
                Vector2 impactDir = Vector2.Normalize(_owner.Position - unit.Position);
                AddParticleTarget(_owner, unit, "velkoz_base_r_hit.troy", unit,
                    lifetime: 0.5f,
                    direction: new Vector3(impactDir.X, 0f, impactDir.Y),
                    flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);
                unit.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
            }
        }

        public void OnSpellChargeCancel(Spell spell, ChannelingStopSource reason)
        {
            // Auto-expire (TimeCompleted): natural channel end at ChannelDuration=2.6s.
            Cleanup();
        }

        public void OnSpellChargeFire(Spell spell)
        {
            // Player early-recast (after CancelChargeOnRecastTime grace of 0.75s).
            // Vel'Koz R has no "fire" payload — release just ends the channel early.
            // Same cleanup as Cancel.
            Cleanup();
        }

        private void Cleanup()
        {
            // Status restore (CanMove/CanCast/CanAttack) is handled by the engine channel
            // pipeline (Spell.ReleaseChannelStatusLock) on channel-end — not here anymore.
            _owner.RemoveBuffsWithName("VelkozR");
            RemoveParticles();
            RemoveMarker();
        }

        private void RemoveMarker()
        {
            if (_laserTarget != null)
            {
                _laserTarget.SetToRemove();
                _laserTarget = null;
            }
        }

        private void RemoveParticles()
        {
            RemoveParticle(_eye);
            RemoveParticle(_lens);
            RemoveParticle(_beam);
            RemoveParticle(_beamEnd);
            RemoveParticle(_lensbeamC);
            RemoveParticle(_lensbeamL);
            RemoveParticle(_lensbeamR);
        }
    }
}