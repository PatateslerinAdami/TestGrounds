using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    // Sion Q — Decimating Smash (4.20). Charge spell, direction locked at cast, no missile:
    // damage/CC applied server-side in a rectangle at release. Fully replay-derived, see
    // docs/SIONQ_WIRE_EXTRACTION.md (test replay e5b335cb, 43 charge sessions).
    public class SionQ : ISpellScript
    {
        // Everything scales in 0.25s charge steps. Damage caps at 1s (step 4), area and CC keep
        // scaling to the 2s auto-fire (step 8). Replay: dmg(1.2s)==dmg(1.65s); stun durations
        // only ever {1.25,1.5,1.75,2.25}.
        private const float StepSeconds = 0.25f;
        private const float SlamThreshold = 1.0f;

        // Hit area (Riot hitbox diagram + wiki + wire): a charge-scaled CIRCLE around Sion
        // intersected with a cone whose apex sits 800u BEHIND him (16° total). The target's
        // CENTER must be inside the circle; its EDGE must touch the yellow cone (radius 1550
        // from the apex = SionQDamage CastConeDistance) and must NOT touch the white cone
        // (radius 720 = SionQShapeCut CastConeDistance — cuts out everything behind Sion).
        // Circle radius per 0.25s step: 250 + 87.5·k, capped at 750 — wire-verified via the
        // flail far-end missiles, whose travel distances are exactly quantized at
        // 337.4/424.9/512.4 (= 250+87.5k), and slam hit distances (600 @ k4, ≤750 later).
        private const float RadiusBase = 250f;
        private const float RadiusStep = 87.5f;
        private const float RadiusMax = 750f;
        private const float ConeApexBehind = 800f;
        private const float ConeHalfAngle = 8f * MathF.PI / 180f;
        private const float YellowConeRadius = 1550f;
        private const float WhiteConeRadius = 720f;

        private ObjAIBase _sion;
        private Spell _spell;
        private Vector2 _direction = new Vector2(1, 0);
        private Buff _chargeBuff;
        private readonly List<Particle> _chargeFx = [];
        private bool _slamReadyFxSpawned;
        private bool _moveLockHeld;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            // SionQ.lua metadata: NotSingleTargetSpell + DoesntBreakShields,
            // DoesntTriggerSpellCasts=false. Damage rides on SionQDamage data.
            NotSingleTargetSpell = true,
            TriggersSpellCasts = true,
            IsDamagingSpell = true,
            // REQUIRED with AutoFaceDirection=false: without this the engine overwrites
            // CastInfo.TargetPosition/End with a fake point 10u in CURRENT facing before the
            // CastSpellAns (Spell.cs:796) — OnSpellChargeStart would then read the old facing
            // instead of the cursor and Sion never turns. Riot's wire carries the RAW cursor
            // in the cont=0 CastSpellAns tpos (~10000u away), so keeping it is also wire-true.
            //OverrideTargetPositionInScript = true,
            // ChargeDuration falls back to SionQ.json ChannelDuration = 2.0 (wire
            // DesignerTotalTime=2.0, client bar fills over the full 2s hold).
            ChargeMaxHoldDuration = 2f,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _sion = owner;
            _spell = spell;
            ApiEventManager.OnUpdateStats.AddListener(this, _sion, OnUpdateStats);
        }

        private void OnUpdateStats(AttackableUnit unit, float diff)
        {
            // Tooltip AD scaling must mirror the ACTUAL per-rank damage (SionQDamage.E3),
            // NOT the flat SionQ.E3/E6 which are the max-rank/tooltip reference. Same rank
            // indexing as Release/ApplyHits so var 0/1 always equal what the spell deals.
            // Min charge = E3·AD (mult 1); max charge = ×3 (damage mult caps at 1s). E6 is
            // deliberately unused: in SionQDamage it is stored in percent units (120-180),
            // so the max value is derived as adMin·3. Replay-verified per-rank
            // (project_sionq_ad_ratio_per_rank_replay_verified).
            var dmgData = _sion.GetSpell("SionQDamage").SpellData;
            int rank = Math.Clamp((int)_spell.CastInfo.SpellLevel, 1, 5);
            float adMin = _sion.Stats.AttackDamage.Total * dmgData.EffectLevelAmount[3][rank];
            float adMax = adMin * 3f;
            SetSpellToolTipVar(_sion, 0, adMin, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
            SetSpellToolTipVar(_sion, 1, adMax, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            // NO SetStatus(CanMove) here: OnSpellChargeStart takes the (ref-counted) move hold in
            // the same tick (castTime=0), and CleanUpCharge releases exactly ONE hold — a second
            // hold here would leak and leave Sion permanently unable to move after the Q.
            owner.StopMovement(networked: false);
        }

        public void OnSpellChargeStart(Spell spell)
        {
            _slamReadyFxSpawned = false;

            // Direction is locked at cast start — replay: release FaceDirection is angle-identical
            // to the charge-start FaceDirection in every session, regardless of cursor movement.
            var cursor = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);
            var dir = cursor - _sion.Position;
            if (dir.LengthSquared() > 0.001f)
            {
                _direction = Vector2.Normalize(dir);
            }
            else
            {
                var facing = new Vector2(_sion.Direction.X, _sion.Direction.Z);
                _direction = facing.LengthSquared() > 0.001f ? Vector2.Normalize(facing) : new Vector2(1, 0);
            }

            FaceDirection(_sion.Position + _direction * 100f, _sion);

            // Sion CANNOT move while charging: 140 strictly-paired charges across 4 real-game
            // replays + 40 test-replay charges show zero movement, ever. SionQ.json's
            // CanMoveWhileChanneling=1 keeps the engine from locking movement itself (and from
            // move-cancelling — move clicks are simply ignored, the charge continues), so the
            // script holds the ref-counted CanMove disable for the charge duration.
            _sion.StopMovement();
            // The ref-counted CanMove hold is required: CanMove()'s channel clause passes for
            // CanMoveWhileChanneling=1 spells, so without this a re-path (right-click) during
            // the charge WOULD move Sion server-side. Released in CleanUpCharge on all end paths.
            _sion.SetStatus(StatusFlags.CanMove, false);
            _moveLockHeld = true;

            // Wire: SetAnimStates {ATTACK1 -> Spell1_CHRG}; the Spell1 charge loop itself is
            // client-driven from the charge cast.
            OverrideAnimation(_sion, "Spell1_CHRG", "ATTACK1", this);

            // Hidden COMBAT_ENCHANCER charge buff, dur 2.0 (wire slot-tracking buff).
            _chargeBuff = AddBuff("SionQ", spell.GetMaxHoldDuration(), 1, spell, _sion, _sion);

            // Charge FX (killed on fire/cancel). The generic marker FX 0x0ce11549 from the wire
            // is unresolved (also appears on E casts) and intentionally skipped.
            // Wire (sionq_fx audit, all bind=Sion): ground indicators carry the LOCKED direction
            // as orientation (flags 0x0030 = UpdateOrientation|SimulateWhileOffScreen, bone
            // BUFFBONE_GLB_GROUND_LOC, TargetNetID=0 -> SpellEffectCreate); Cas is target-bound to Sion
            // on the ground bone (0x0020, TargetNetID=Sion -> SpellEffectCreate); the weapon glow
            // sits on Buffbone_Glb_Weapon_1 (0x0020).
            float fxLife = spell.GetMaxHoldDuration() + 0.5f;
            var dir3 = new Vector3(_direction.X, 0, _direction.Y);
            _chargeFx.Add(SpellEffectCreate("Sion_Base_Q_Indicator.troy",_sion, _sion, null, _sion.Position, lifetime: fxLife,
                boneName: "BUFFBONE_GLB_GROUND_LOC", orientTowards: dir3,
                flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen));
            _chargeFx.Add(SpellEffectCreate("Sion_Base_Q_Indicator2.troy",_sion, _sion,  null, _sion.Position, lifetime: fxLife,
                boneName: "Buffbone_Glb_Ground_Loc", orientTowards: dir3,
                flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen));
            _chargeFx.Add(SpellEffectCreate("Sion_Base_Q_Cas.troy",_sion, _sion,  _sion, lifetime: fxLife,
                boneName: "Buffbone_Glb_Ground_Loc", flags: FXFlags.SimulateWhileOffScreen));
            _chargeFx.Add(SpellEffectCreate("Sion_Base_Q_Cas1_weapon.troy",_sion, _sion,  _sion, lifetime: fxLife,
                boneName: "Buffbone_Glb_Weapon_1", flags: FXFlags.SimulateWhileOffScreen));
        }

        public void OnSpellChargeUpdate(Spell spell, Vector3 position, bool forceStop)
        {
            // Direction is locked — no steering (unlike Vel'Koz R / Sion R marker sweeps).
        }

        public void OnSpellChargeTick(Spell spell, float diff)
        {
            // "Slam ready" at charge >= 1s: red indicator + weapon glow spawn ON TOP of the
            // white set (wire kills the white FX only at release, not here).
            if (_slamReadyFxSpawned || !(spell.GetChargeElapsed() >= SlamThreshold)) return;
            _slamReadyFxSpawned = true;
            // Wire: red indicators + Cas3 like the white indicators (0x0030, GROUND_LOC,
            // orientation = locked direction; Cas3 at scale 1.5); weapon glow upgrade on
            // Buffbone_Glb_Weapon_1 (0x0020, target-bound).
            var fxLife = spell.GetMaxHoldDuration() + 0.5f;
            var dir3 = new Vector3(_direction.X, 0, _direction.Y);
            _chargeFx.Add(SpellEffectCreate("Sion_Base_Q_Indicator_Red.troy", _sion, _sion, null, _sion.Position,
                lifetime: fxLife,
                boneName: "Buffbone_Glb_Ground_Loc", orientTowards: dir3,
                flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen));
            _chargeFx.Add(SpellEffectCreate("Sion_Base_Q_Cas3.troy", _sion, _sion, null, _sion.Position,
                lifetime: fxLife, scale: 1.5f,
                boneName: "Buffbone_Glb_Ground_Loc", orientTowards: dir3,
                flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen));
            _chargeFx.Add(SpellEffectCreate("Sion_Base_Q_Cas2_weapon.troy", _sion, _sion, _sion, lifetime: fxLife,
                boneName: "Buffbone_Glb_Weapon_1", flags: FXFlags.SimulateWhileOffScreen));
            _chargeFx.Add(SpellEffectCreate("Sion_Base_Q_Indicator_Red2.troy", _sion, _sion, null, _sion.Position,
                lifetime: fxLife,
                boneName: "Buffbone_Glb_Ground_Loc", orientTowards: dir3,
                flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen));
        }

        // Manual recast release. The engine routed the recast through UpdateCharge ->
        // FinishChanneling and set the full cooldown; the cont=1 CastSpellAns is emitted here
        // (SpellCastCharge for the flail-missile path, NotifyChargeFireCastSpellAns otherwise).
        public void OnSpellChargeFire(Spell spell)
        {
            Release(spell, autoFire: false);
        }

        public void OnSpellChargeCancel(Spell spell, ChannelingStopSource reason)
        {
            if (reason == ChannelingStopSource.TimeCompleted)
            {
                // Held to the 2s cap -> AUTO-FIRE. Riot wire: full release effects, but NO second
                // CastSpellAns — only the NPC_InstantStop_Attack the engine's ExpireCharge already
                // sent. Full cooldown is set by ExpireCharge after this handler.
                Release(spell, autoFire: true);
                return;
            }

            // Real interrupt (CC / death / another cast — move clicks neither move nor cancel:
            // the script holds CanMove and CanMoveWhileChanneling=1 keeps the engine's
            // move-cancel check off). Wire: ISA (engine) + FX_Kill + BuffRemove + anim restore,
            // no damage. Description: 2-second cooldown.
            CleanUpCharge();
            // Deferred one tick: during this event State is still STATE_CHANNELING, so an
            // immediate SetCooldown would skip the STATE_COOLDOWN transition and StopChanneling
            // would leave the spell READY with a dangling cooldown.
            CreateTimer(0f, () => spell.SetCooldown(2f, true));
        }

        private void Release(Spell spell, bool autoFire)
        {
            float charge = spell.GetChargeElapsed();
            int step = Math.Min(8, (int)(charge / StepSeconds));
            bool slam = charge >= SlamThreshold;
            // Grade-"3" FX set only at the full 2s charge (replay: manual 1.81s still used "2").
            bool full = autoFire || charge >= spell.GetMaxHoldDuration();
            float radius = Math.Min(RadiusMax, RadiusBase + RadiusStep * step);

            CleanUpCharge();

            Vector2 ownerPos = _sion.Position;
            FaceDirection(ownerPos + _direction * 100f, _sion);
            // Riot wire (all releases): ScaleTime=0, StartProgress=0, SpeedRatio=1, flags=0x05.
            // Our params map scaleTime->ScaleTime / scaleSpeed->SpeedRatio, and the API defaults
            // (scaleTime=1, scaleSpeed=0) send the OPPOSITE of Riot — SpeedRatio=0 is what made
            // the animation play in slow motion.
            PlayAnimation(_sion, slam ? "Spell1_Hit2" : "Spell1_Hit1",
                scaleTime: 0f, startProgress: 0f, scaleSpeed: 1f,
                flags: AnimationFlags.Lock | AnimationFlags.NoBlend);

            // Riot: Sion is locked out of all actions for 0.25s on release (the wire ISA at
            // ~+0.25 marks its end). W is EXEMPT (castable during the lockout), so we can only
            // hold CanAttack — E/R being castable 0.25s early is the accepted gap.
            _sion.SetStatus(StatusFlags.CanAttack, false);
            CreateTimer(0.25f, () => _sion.SetStatus(StatusFlags.CanAttack, true));

            AddBuff(slam ? "SionQSoundAfterHalf" : "SionQSoundBeforeHalf", 0.25f, 1, spell, _sion, _sion);

            var hits = GetTargetsInArea(spell, ownerPos, radius);
            ApplyHits(spell, hits, step, slam, full);

            var grade = full ? "3" : slam ? "2" : "1";
            // Caster-bound swing/impact FX — Hit2/Hit3 play on slam even on a whiff (wire 848.77/1512.23).
            // Wire: Hit FX = 0x0030, bone BUFFBONE_GLB_GROUND_LOC, orientation = locked direction,
            // bind=Sion, TargetNetID=0; Cas3_weapon = 0x0020 on Buffbone_Glb_Weapon_1, target-bound.
            if (slam)
            {
                SpellEffectCreate($"Sion_Base_Q_Hit{grade}.troy", _sion, _sion, null, ownerPos,
                    boneName: "BUFFBONE_GLB_GROUND_LOC",
                    orientTowards: new Vector3(_direction.X, 0, _direction.Y),
                    flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);
                if (full)
                {
                    SpellEffectCreate("Sion_Base_Q_Cas3_weapon.troy", _sion, _sion, _sion,
                        boneName: "Buffbone_Glb_Weapon_1", flags: FXFlags.SimulateWhileOffScreen);
                }
            }

            if (!slam)
            {
                // Flail: cosmetic SionQHitParticleMissile2 (ExtraSpell4 -> slot 48) flies to the
                // FAR END of the hitbox along the locked direction — hit or whiff (wiki: "enemies
                // may see a particle at the far end of the hitbox"). Wire-verified: travel distance
                // == the current area radius exactly (512.4 vs 512.5 etc.). SpellCastCharge also
                // emits the charge-exit cont=1 CastSpellAns. Flail cannot happen on auto-fire.
                SpellCastCharge(spell, 3, SpellSlotType.ExtraSlots, ownerPos,
                    ownerPos + _direction * radius, fireWithoutCasting: true);
            }
            else if (!autoFire)
            {
                // Manual slam: no missile, but the client still needs the charge-exit
                // cont=1 CastSpellAns. Auto-fire sends NOTHING (engine ISA(0) already went out).
                var endPos = ownerPos + _direction * radius;
                spell.NotifyChargeFireCastSpellAns(new Vector3(endPos.X, _sion.GetHeight(), endPos.Y));
            }
        }

        private List<AttackableUnit> GetTargetsInArea(Spell spell, Vector2 ownerPos, float radius)
        {
            // Riot hitbox (hitbox diagram + wiki): target CENTER inside the charge-scaled circle
            // around Sion, target EDGE touching the 16° yellow cone (apex 800 behind Sion, radius
            // 1550) and NOT touching the white cone (radius 720, i.e. reaching behind Sion).
            // No hit cap — the ChainMissileParameters MaximumHits=4 block is vestigial
            // (7-target knockup observed in the replay).
            Vector2 apex = ownerPos - _direction * ConeApexBehind;

            return (from unit in GetUnitsInRange(_sion, ownerPos, radius + 50f, true, spell.SpellData.Flags)
                where unit != _sion
                where !(Vector2.Distance(unit.Position, ownerPos) > radius)
                let bb = unit.CollisionRadius
                where CircleTouchesSector(unit.Position, bb, apex, _direction, YellowConeRadius)
                where !CircleTouchesSector(unit.Position, bb, apex, _direction, WhiteConeRadius)
                select unit).ToList();
        }

        // Circle (unit center + bounding radius) vs cone sector (apex, ±ConeHalfAngle around
        // dir, given radius). Sector boundary = two edge segments + the arc; a circle outside
        // the wedge angle can only reach the sector via an edge segment.
        private static bool CircleTouchesSector(Vector2 center, float r, Vector2 apex, Vector2 dir, float radius)
        {
            var v = center - apex;
            float d = v.Length();
            if (d - r > radius)
            {
                return false;
            }

            if (d < 0.001f)
            {
                return true;
            }

            float angle = MathF.Acos(Math.Clamp(Vector2.Dot(dir, v) / d, -1f, 1f));
            if (angle <= ConeHalfAngle)
            {
                return true;
            }

            var e1 = Rotate(dir, ConeHalfAngle);
            var e2 = Rotate(dir, -ConeHalfAngle);
            return DistPointSegment(center, apex, apex + e1 * radius) <= r
                   || DistPointSegment(center, apex, apex + e2 * radius) <= r;
        }

        private static Vector2 Rotate(Vector2 v, float rad)
        {
            float c = MathF.Cos(rad);
            float s = MathF.Sin(rad);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        private static float DistPointSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }

        private void ApplyHits(Spell spell, List<AttackableUnit> hits, int step, bool slam, bool full)
        {
            int rank = Math.Clamp((int)spell.CastInfo.SpellLevel, 1, 5);
            // Base = SionQ E1 (20-100), total-AD ratio = SionQDamage E3 (0.40-0.60); both scale
            // x1 -> x3 with charge, capped at 1s. Replay fit: rank2 flail step2 -> 120.5 observed
            // vs 118 predicted; rank2 slam -> 154.9 vs 155.
            float mult = 1f + 0.5f * Math.Min(step, 4);
            var dmgData = _sion.GetSpell("SionQDamage").SpellData;
            float baseDamage = spell.SpellData.EffectLevelAmount[1][rank];
            float adRatio = dmgData.EffectLevelAmount[3][rank];
            float damage = (baseDamage + adRatio * _sion.Stats.AttackDamage.Total) * mult;

            // Stun total 1.25 + 0.25/step above 1s (2.25 only at the full 2s); the airborne
            // portion is 0.5 + 0.125/step, the remainder is a separate visible Stun on landing
            // (handled by the SionQKnockUp buff script).
            int ccStep = Math.Max(0, step - 4);
            float stunTotal = 1.25f + 0.25f * ccStep;
            float knockupTime = 0.5f + 0.125f * ccStep;

            var tarFx = full ? "Sion_Base_Q3_tar.troy" : slam ? "Sion_Base_Q2_tar.troy" : "Sion_Base_Q1_tar.troy";

            foreach (var target in hits)
            {
                // 4.20: the 0.6 modifier applies to minions AND monsters (the live 165%-vs-monsters
                // carve-out shipped in a later patch; Monster derives from Minion here so one check
                // covers both). SionQ.json E3 = 0.6 all ranks.
                float dealt = damage;
                if (target is Minion)
                {
                    dealt *= spell.SpellData.EffectLevelAmount[3][0];
                }

                target.TakeDamage(_sion, dealt, DamageType.DAMAGE_TYPE_PHYSICAL,
                    DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);

                // Per-hit sound-carrier FX (wire, every release with hits, flail AND slam):
                // EffectNameHash=0 (empty name), flags=0, no bones, TargetNetID=0, bind=target,
                // KeywordNetID=Sion. Drives the client's on-hit/material audio for the target.
                SpellEffectCreate("", _sion, target, null, target.Position, flags: FXFlags.None, keywordObject: _sion);

                // Wire: Qx_tar bound to the target (bind=tgt=unit, no bone, scale 2). The FLAIL
                // variant (Q1_tar) additionally carries orientation = MINUS the cast direction
                // (0x0030) — the hit flash faces back along the swing; Q2/Q3_tar are plain 0x0020.
                if (slam)
                {
                    SpellEffectCreate(tarFx, _sion, target, target, scale: 2f, flags: FXFlags.SimulateWhileOffScreen);
                }
                else
                {
                    SpellEffectCreate(tarFx, _sion, target, target, scale: 2f,
                        orientTowards: new Vector3(-_direction.X, 0, -_direction.Y),
                        flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);
                }

                if (slam)
                {
                    // Slam applies BOTH hit-sound buffs (wire), then the knockup carrying the CC.
                    // No tenacity math here: the KNOCKUP buff type is not tenacity-reducible
                    // (engine mask), and the landing-stun tail is applied by the buff script from
                    // the forced-movement end event (PulverizeSpeed pattern) as a normal Stun —
                    // the engine reduces THAT by tenacity once, at add time.
                    AddBuff("SionQSoundAfterHalfHit", 0.25f, 1, spell, target, _sion);
                    AddBuff("SionQSoundBeforeHalfHit", 0.25f, 1, spell, target, _sion);
                    var vars = new VariableTable();
                    vars.Set("KnockupTime", knockupTime);
                    vars.Set("StunTail", stunTotal - knockupTime);
                    if (target.IsDead)return;
                    AddBuff("SionQKnockUp", stunTotal, 1, spell, target, _sion, variableTable: vars);
                }
                else
                {
                    AddBuff("SionQSoundBeforeHalfHit", 0.25f, 1, spell, target, _sion);
                    if (target.IsDead)return;
                    AddBuff("SionQSlow", 0.3f, 1, spell, target, _sion);
                }
            }
        }

        private void CleanUpCharge()
        {
            if (_moveLockHeld)
            {
                _moveLockHeld = false;
                _sion.SetStatus(StatusFlags.CanMove, true);
            }

            ClearOverrideAnimation(_sion, "ATTACK1", this);
            if (_chargeBuff != null)
            {
                _sion.RemoveBuff(_chargeBuff);
                _chargeBuff = null;
            }

            foreach (var fx in _chargeFx)
            {
                fx?.SetToRemove();
            }

            _chargeFx.Clear();
        }
    }

    // Cosmetic flail-hit missile (ExtraSpell4, wire slot 48): line missile at speed 2000 carrying
    // Sion_Base_Q_Hit1.troy to the hit target's position (LineMissileEndsAtTargetPoint=1).
    // Spawned by SionQ.Release via SpellCastCharge; deals no damage.
    public class SionQHitParticleMissile2 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Arc
            },
            TriggersSpellCasts = false,
            IsDamagingSpell = false
        };
    }

    public class SionQSoundAfterHalf : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false,
            IsDamagingSpell = true
        };
    }

    public class SionQDamage : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false,
        };
    }

    public class SionQShapeCut : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false,
        };
    }

    public class SionQHitParticleMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false,
        };
    }

    public class SionQIndicatorMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false,
        };
    }

    public class SionQIndicatorMissile2 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false,
        };
    }
}