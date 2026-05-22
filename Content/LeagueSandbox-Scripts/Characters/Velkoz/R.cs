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
            ChargeDuration = 2.6f,
            TriggersSpellCasts = false,
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

        private float _currentAngle;
        private float _targetAngle;
        private PeriodicTicker _damageTicker;

        // Beam line dimensions (units). Range matches SpellData.CastRange (1550).
        // Half-width 80 is a reasonable approximation of the visible beam thickness;
        // tune against in-game feel if needed.
        private const float BeamRange = 1550f;
        private const float BeamHalfWidth = 80f;

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
        }

        public void OnSpellChargeStart(Spell spell)
        {
            _damageTicker.Reset();

            // Caster is already turned toward the cast direction by OnSpellPreCast's
            // FaceDirection call. _owner.Direction is the authoritative source for the
            // beam's initial heading — `spell.CastInfo.TargetPosition` isn't reliable at
            // this stage of the pipeline (was empty/stale at OnSpellChargeStart time).
            Vector2 dir = new Vector2(_owner.Direction.X, _owner.Direction.Z);
            _currentAngle = (float)Math.Atan2(dir.Y, dir.X);
            _targetAngle = _currentAngle;

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

            // Replay-verified wake-up: Riot ALWAYS sends a self-noop S2C_MoveMarker
            // (Position == Goal) immediately after SpawnMarkerS2C, BEFORE the
            // FX_Create_Group bundle goes out. The three-packet sequence at +510997ms
            // is: SpawnMarkerS2C → S2C_MoveMarker → FX_Create_Group. Without the
            // intermediate MoveMarker, the .troy fails to bind particles to the marker.
            _laserTarget.MoveTo(_laserTarget.Position);

            // Wake-up MoveMarker and first damage tick fire on the first OnSpellChargeTick
            // (handled via _firstTickFired). Thematically: a laser is instant — no 250ms
            // grace period before the first damage. Riot's replay matches: SpawnMarkerS2C
            // followed by S2C_MoveMarker ~30ms later (effectively the first tick).

            // VelkozR.json has CanMoveWhileChanneling=0 — caster is rooted for the
            // duration. Also block casting/attacking to match the channel-lock pattern.
            _owner.StopMovement();
            _owner.SetStatus(StatusFlags.CanMove, false);
            _owner.SetStatus(StatusFlags.Rooted, true);
            _owner.SetStatus(StatusFlags.CanCast, false);
            _owner.SetStatus(StatusFlags.CanAttack, false);

            // Spell4_Cast is the 4.x ultimate-channel animation for Vel'Koz. It loops for
            // the channel duration; the natural transition handles end-of-spell.
            PlayAnimation(_owner, "Spell4_Cast");

            // Replay-verified: VelkozR buff applies ~292ms after the press packet,
            // matching the Spell4_Cast wind-up animation. Lifetime shortened so it
            // expires at the same wall-clock moment as the rest of the spell.
            _owner.RegisterTimer(new GameScriptTimer(0.29f, () =>
            {
                AddBuff("VelkozR", 2.31f, 1, spell, _owner, _owner);
            }));

            _eye = AddParticleTarget(_owner, _owner, "velkoz_base_r_beam_eye.troy", _owner, lifetime: 2.6f, bone: "Buffbone_Cstm_EyeBall", targetBone: "Buffbone_Cstm_EyeBallTarget");
            _lens = AddParticle(_owner, _owner, "velkoz_base_r_lens.troy", _owner.Position, lifetime: 2.6f, bone: "Buffbone_Cstm_EyeBallTarget");

            // Replay wire for _R_beam needs two distinct world coords: Pos.xz = caster (so
            // the .troy resolves the start to the EyeBallTarget bone) and Target.xz = marker
            // endpoint. AddParticleTarget(bindObj+target) would collapse both onto the marker
            // (rendering only a point at the endpoint), so use the 2-arg overload to keep them
            // distinct, then patch TargetNetID via override.
            _beam = AddParticleTarget(_owner, _owner, "velkoz_base_r_beam.troy", _laserTarget, lifetime: 2.6f, bone: "Buffbone_Cstm_EyeBallTarget", targetBone: "root");
            // Replay wire for _R_beam_end (InspectorGadget dump):
            //   BindNetID=marker, TargetNetID=0, CasterNetID=Vel'Koz, KeywordNetID=0
            //   Pos.xz = marker pos, Pos.y = 51.43 (terrain)
            //   Target.xz = (-3679,-3706) int16 = off-map sentinel, Target.y = 0
            //   Owner.xz = marker pos, Owner.y = 51.43
            //   Bone=0, TargetBone=0, Flags=BindDirection (0x20), Orient=(0,0,0)
            //
            // The off-map Target with Y=0 is replicated via:
            //   - passing off-map Vector2 as targetPos (drives Target.xz int16-encoded
            //     to -3679,-3706)
            //   - followGroundTilt:true makes the builder write Target.y =
            //     GetHeightAtLocation(StartPosition) ≈ 0 for off-map coords
            var offMapTarget = new Vector2(-367f, -421f); // int16-encodes to (-3679,-3706)
            _beamEnd = AddParticle(_owner, _laserTarget, "velkoz_bas e_r_beam_end.troy", offMapTarget,
                lifetime: 2.6f, followGroundTilt: true);
            _beamEnd.OwnerPositionOverride = new Vector3(_laserTarget.Position.X, _laserTarget.GetHeight(), _laserTarget.Position.Y);
            _beamEnd.KeywordNetIDOverride = 0;

            _lensbeamC = AddParticleTarget(_owner, _owner, "velkoz_base_r_lensbeam.troy", _owner, lifetime: 2.6f, bone: "C_Buffbone_Cstm_Tenticle", targetBone: "Buffbone_Cstm_EyeBallTarget");
            _lensbeamL = AddParticleTarget(_owner, _owner, "velkoz_base_r_lensbeam.troy", _owner, lifetime: 2.6f, bone: "L_Buffbone_Cstm_Tenticle", targetBone: "Buffbone_Cstm_EyeBallTarget");
            _lensbeamR = AddParticleTarget(_owner, _owner, "velkoz_base_r_lensbeam.troy", _owner, lifetime: 2.6f, bone: "R_Buffbone_Cstm_Tenticle", targetBone: "Buffbone_Cstm_EyeBallTarget");
        }

        public void OnSpellChargeUpdate(Spell spell, Vector3 position, bool forceStop)
        {
            Vector2 targetDir = Vector2.Normalize(new Vector2(position.X, position.Z) - _owner.Position);
            _targetAngle = (float)Math.Atan2(targetDir.Y, targetDir.X);
        }

        public void OnSpellChargeTick(Spell spell, float diff)
        {
            // --- Steering (unchanged): rotate _currentAngle toward cursor at turn rate.
            float angleDiff = _targetAngle - _currentAngle;
            while (angleDiff > Math.PI) angleDiff -= (float)(2 * Math.PI);
            while (angleDiff < -Math.PI) angleDiff += (float)(2 * Math.PI);

            if (Math.Abs(angleDiff) > 0.001f)
            {
                float turnRate = 0.8f;
                float step = turnRate * (diff / 1000f);

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

                // Steer the beam: rotate the caster's bone AND move the endpoint marker.
                // The beam particle binds to both ends (caster bone start + marker end),
                // so both need to update each tick. Replay-verified for Vel'Koz R:
                // S2C_MoveMarker packets at ~100ms intervals, Speed=1033, FaceGoal=true.
                _owner.FaceDirection(new Vector3(newDir.X, 0, newDir.Y), true);
                _laserTarget.MoveTo(_owner.Position + newDir * BeamRange);
            }

            // --- Damage ticks: laser is instantaneous — first damage tick fires on the
            // first OnSpellChargeTick (no 250ms grace), then every DamageTickIntervalMs.
            var ticks = _damageTicker.ConsumeTicks(diff, DamageTickIntervalMs, fireImmediately: true);
            for (var i = 0; i < ticks; i++)
            {
                ApplyBeamDamage(spell);
            }
        }

        private void ApplyBeamDamage(Spell spell)
        {
            Vector2 dir = new Vector2((float)Math.Cos(_currentAngle), (float)Math.Sin(_currentAngle));
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
                    flags: FXFlags.GivenDirection | FXFlags.BindDirection);
                unit.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
            }
        }

        public void OnSpellChargeCancel(Spell spell, ChannelingStopSource reason)
        {
            _owner.RemoveBuffsWithName("VelkozR");
            RestoreStatus();
            RemoveParticles();
            RemoveMarker();
        }

        public void OnSpellChargeFire(Spell spell)
        {
            RestoreStatus();
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

        private void RestoreStatus()
        {
            _owner.SetStatus(StatusFlags.CanMove, true);
            _owner.SetStatus(StatusFlags.Rooted, false);
            _owner.SetStatus(StatusFlags.CanCast, true);
            _owner.SetStatus(StatusFlags.CanAttack, true);
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