using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using System;
using System.Collections.Generic;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class XerathArcanopulseChargeUp : ISpellScript
    {
        // HUD line targeter (SpellTargeter2) in XerathArcanopulseChargeUp.json:
        //   OverrideBaseRange = 700, RangeGrowthMax = 1400, RangeGrowthDuration = 1.5s
        // The JSON's SpellData CastRange (750) / CastRangeGrowthMax (1700) overshoot the
        // visible line indicator by 50/300 units respectively, but Riot's gameplay range
        // matches the LINE targeter — replay-verified: chain length at full charge =
        // exactly 1400u = SpellTargeter2.RangeGrowthMax, step = 100u. Our SpellData parser
        // doesn't read SpellTargeter2 so we compute the range from these constants.
        private const float HudLineMinRange = 700f;
        private const float HudLineMaxRange = 1400f;
        private const float HudLineGrowthDuration = 1.5f;

        private ObjAIBase _xerath;
        private Buff _soundBuff;
        private Particle _chargeFx;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            // ChargeDuration is resolved at runtime by GetEffectiveChannelDuration from
            // XerathArcanopulseChargeUp.json SpellTargeter1.RangeGrowthDuration = 1.5
            // (also matches CastRangeGrowthDuration = 1.5). ChargeMaxHoldDuration = 3s is
            // hardcoded — JSON ChannelDuration is 3.0 but that's the same as our hold cap,
            // so we keep the explicit script value for clarity.
            ChargeMaxHoldDuration = 3f,
            TriggersSpellCasts = false,
            IsDamagingSpell = true,
            AutoFaceDirection = false
        };


        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _xerath = owner;
        }

        public void OnSpellChargeStart(Spell spell)
        {
            _soundBuff = AddBuff("XerathArcanopulseChargeUp", spell.GetMaxHoldDuration(), 1, spell, _xerath, _xerath);

            // Charge-up particle — the "energy gathering" visual that plays from cast-press
            // through charge-fire. Replay wire (3 samples idx 9441 / 11919 / 14302):
            //   TargetNetID = BindNetID = CasterNetID = KeywordNetID = Xerath
            //   Pos = Target = Owner = Xerath.Position
            //   Flags = 0x0020 (BindDirection)
            //   Bone = BUFFBONE_GLB_CHANNEL_LOC (0x004b3653) — attaches to Xerath's spell-channel bone
            // AddParticleTarget(target=_xerath) is required to set TargetNetID=Xerath; the
            // older AddParticle path would leave TargetNetID=0.
            _chargeFx = AddParticleTarget(_xerath, _xerath, "Xerath_Base_Q_cas_charge.troy", _xerath,
                lifetime: spell.GetMaxHoldDuration(), bone: "BUFFBONE_GLB_CHANNEL_LOC");
        }

        public void OnSpellChargeUpdate(Spell spell, Vector3 position, bool forceStop)
        {
            if (!forceStop)
            {
                FaceDirection(new Vector2(position.X, position.Z), _xerath, false);
            }
        }

        public void OnSpellChargeCancel(Spell spell, ChannelingStopSource reason)
        {
            LetGo();

            float manaCost = spell.SpellData.ManaCost[spell.CastInfo.SpellLevel];
            _xerath.Stats.CurrentMana += manaCost * 0.5f;
        }

        public void OnSpellChargeFire(Spell spell)
        {
            // Use the HUD line targeter values (700 → 1400 over 1.5s) instead of
            // spell.GetCurrentChargeRange() — which reads CastRangeGrowthMax=1700 from
            // SpellData and overshoots the visible HUD line by 300u. Replay-verified:
            // chain length at full charge = 1400u, step = 100u.
            float elapsed = spell.GetChargeElapsed();
            float progress = Math.Clamp(elapsed / HudLineGrowthDuration, 0f, 1f);
            float currentRange = HudLineMinRange + (HudLineMaxRange - HudLineMinRange) * progress;

            Vector2 ownerPos = _xerath.Position;
            Vector2 mousePos = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);

            Vector2 direction = Vector2.Normalize(mousePos - ownerPos);
            if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
            {
                direction = new Vector2(1, 0);
            }

            Vector2 castPos = ownerPos + (direction * currentRange);

            FaceDirection(castPos, _xerath, true);

            // Sector-style charge-fire: full SpellCast on XerathArcanopulse2 (ExtraSpell1 = slot
            // 45) so its OnSpellPreCast hook fires (sector creation, particles, anim, status
            // flags). Plus parent channel-end NPC_CastSpellAns(slot=0, Unknown1=true) to clear
            // the charge bar. Replay-verified wire shape (Xerath replay 1050f59b).
            // Sector-style charge-fire (fireWithoutCasting=false). Runs full SpellCast pipeline
            // on XerathArcanopulse2 (ExtraSpell1 → slot 0) so its OnSpellPreCast runs (sector
            // + particles + anim + status flags). Plus parent NPC_CastSpellAns(slot=0,
            // Unknown1=true) for charge-bar-clear.
            SpellCastCharge(spell, 0, SpellSlotType.ExtraSlots, ownerPos, castPos,
                fireWithoutCasting: false);

            // Kill the channel-up buff immediately on release, but defer the cas_charge
            // FX kill to align with XerathArcanopulse2.FireBeam (~0.5s = FireLockoutSeconds).
            // The "energy gathering" visual must hold through the post-release wind-up
            // frames; removing it at release makes the spell look truncated and leaves
            // a visible gap before the beam appears.
            _xerath.RemoveBuff(_soundBuff);
            var fx = _chargeFx;
            _chargeFx = null;
            if (fx != null)
            {
                _xerath.RegisterTimer(new GameScriptTimer(0.5f, () => fx.SetToRemove()));
            }
        }

        private void LetGo()
        {
            // Cancel-only path: kill both buff and FX immediately. Fire path schedules
            // its own deferred FX kill — do not call LetGo() from OnSpellChargeFire.
            _xerath.RemoveBuff(_soundBuff);
            if (_chargeFx != null)
            {
                _chargeFx.SetToRemove();
                _chargeFx = null;
            }
        }
    }

    public class XerathArcanopulse2 : ISpellScript
    {
        // Post-release lockout before the beam actually fires. Riot's value (ability tooltip:
        // "Xerath becomes unable to act for 0.528 seconds"). NOT stored in BaseXerath.blnd —
        // Spell1Finish has mEventDataIndex=-1; the value almost certainly lives in
        // CharScriptXerath.luaobj which we don't parse. Hardcoded as a GameScriptTimer because
        // XerathArcanopulse2.json sets InstantCast, which makes the server pipeline fire
        // OnSpellPreCast and OnSpellPostCast same-tick and bypasses ScriptMetadata.CastTime.
        private const float FireLockoutSeconds = 0.5f;

        // Beam visual lifetime.
        private const float BeamLifetimeSeconds = 3.0f;

        // Fixed forward offset (in world units) for the beam's visual emit point. The
        // beam particle's Pos.xz is placed this many units ahead of Xerath along the
        // cast direction, regardless of charge level — so the emit anchor stays at the
        // hand/wand position whether the player cast at min range or max range. Replace
        // the old proportional formula (= startPos - step*2, which scaled with charge).
        private const float BeamEmitOffset = 150f;

        private ObjAIBase _xerath;
        private Spell _spell;
        private Vector2 _end;

        private Vector2 _start;
        private Vector2 _startPos;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _xerath = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            _spell = spell;
            _end = end;
            _start = start;

            owner.StopMovement();
            FaceDirection(end, owner);

            // Fixed forward offset along the cast direction — emit point sits
            // BeamEmitOffset units in front of Xerath regardless of charge level.
            // The `.troy` draws the beam line from `Pos.xz` (= _startPos) to
            // `Target.xz` (= _end) purely from coordinates; no anchor entities
            // required (replay's 16-minion chain was 4.x discrete-sample hit-
            // detection plumbing, see memory project_xerath_q_beam_anchor_chain).
            Vector2 castDir2 = end - start;
            float castDir2Len = castDir2.Length();
            _startPos = castDir2Len > 0.001f
                ? start + castDir2 / castDir2Len * BeamEmitOffset
                : start;

            // PlayAnimation wire — replay-verified across 14 Xerath Spell1Finish casts
            // (idx 9729, 12123, 14653, 17354, ...): flags=0x85 (UniqueOverride|Override|
            // Unknown8), ScaleTime=0 (use animation's intrinsic duration), StartProgress=0,
            // SpeedRatio=1.0. Our previous `PlayAnimation(owner, "Spell1Finish", 1.3f)`
            // sent ScaleTime=1.3 + SpeedRatio=0 + flags=0 — all three fields wrong.
            PlayAnimation(owner, "Spell1Finish",
                timeScale: 0f,
                startTime: 0f,
                speedScale: 1f,
                flags: AnimationFlags.Lock | AnimationFlags.NoBlend | AnimationFlags.Junk7);
            AddParticlePos(_xerath, "xerath_base_q_aoe_reticle_green", _start, _end, 3.0f,
                enemyParticle: "xerath_base_q_aoe_reticle_red");
            // CAS wire (3 replay samples idx 9740/12133/14663 — all consistent):
            //   TargetNetID = BindNetID = CasterNetID = KeywordNetID = Xerath
            //   Pos = Target = Owner = Xerath.Position (all three identical)
            //   Flags = 0x0030 (GivenDirection | BindDirection)
            //   OrientationVector = unit vector in XZ plane = cast direction normalized
            //   bones = empty
            //
            // The OrientationVector is what tells the .troy how to face — without it the
            // hand-emit FX rotates to face nowhere. Pass the actual cast direction so the
            // visual lines up with the aim.
            Vector3 castDir = Vector3.Zero;
            Vector2 d2 = end - start;
            float d2Len = d2.Length();
            if (d2Len > 0.001f)
            {
                castDir = new Vector3(d2.X / d2Len, 0f, d2.Y / d2Len);
            }

            AddParticleTarget(_xerath, _xerath, "xerath_base_q_cas.troy", _xerath,
                lifetime: 3.0f, direction: castDir,
                flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);

            // Long-range warning audio cue for enemies. Sound-only .troy bundled into the
            // same cast-press FX_Create_Group packet as cas+reticle in the replay (idx
            // 9740). Skin-specific file — replay's Xerath was on Skin03 (Battlecast),
            // 116 Skin03 audio packets, 0 Base. Pick the variant matching caster skin so
            // the right sample plays for whatever skin the player has equipped.
            // Wire (replay-verified): CasterNetID=Xerath, BindNetID=0 (free-standing),
            // TargetNetID=0, Position=Xerath.Position, Flags=0x0020 (BindDirection default).
            string warningAudio = _xerath.SkinID switch
            {
                1 => "Xerath_Skin01_Q_LongRange_Warning_Audio.troy",
                2 => "Xerath_Skin02_Q_LongRange_Warning_Audio.troy",
                3 => "Xerath_Skin03_Q_LongRange_Warning_Audio.troy",
                _ => "Xerath_Base_Q_LongRange_Warning_Audio.troy",
            };
            AddParticlePos(_xerath, warningAudio, _xerath.Position, _xerath.Position,
                lifetime: 1f);
            owner.StopMovement();
            // This is the release CAST-RECOVERY lockout (the beam's cast-animation hold), NOT a
            // crowd-control root: Riot applies it automatically as a ref-counted CanMove/CanCast/
            // CanAttack hold for the cast-animation duration (XerathArcanopulse2 is InstantCast with
            // no CastTime in its data). Do NOT set StatusFlags.Rooted — that would mis-classify it as
            // CC (tenacity-reducible / cleansable / root indicator). See project_cast_lockout_vs_cc.
            owner.SetStatus(StatusFlags.CanMove, false);
            owner.SetStatus(StatusFlags.CanCast, false);
            owner.SetStatus(StatusFlags.CanAttack, false);
            
            CreateTimer(0.5f, FireBeam);
        }

        private void FireBeam()
        {
            // Beam wire (AddParticlePos: BindNetID=0, TargetNetID=0, no anchor entities):
            //   Pos.xz      = _startPos   ("from" point; startPos - step*2, ~2 step-lengths
            //                             in front of Xerath. Lifts the visual emit off
            //                             Xerath's feet onto the hand/wand.)
            //   Target.xz   = _end        ("to" point; cast endpoint)
            //   Owner.xz    = Xerath pos  (= _start, server-hardcoded from CasterNetID)
            //
            // The `.troy` draws the beam line purely from Pos.xz and Target.xz coordinates
            // — no entity references needed because the two positions are distinct. Riot's
            // 16-anchor chain + 15-packet FaceDirection burst that we removed was 4.x
            // discrete-sample hit-detection plumbing, not rendering. See memory file
            // project_xerath_q_beam_anchor_chain for the full investigation.
            //
            // overrideTargetHeight = 185f matches replay (Pos.y = ~236.85 with terrain
            // ground ~52 = +185 elevation). Our packet builder writes the same elevated Y
            // to both PositionY and TargetPositionY (Riot splits them) — minor wire
            // mismatch but visually equivalent.
            const float beamElevation = 185f;
            // Keyword=caster: replay 1050f59b shows all 117 beam packets carry KeywordNetID=Xerath.
            // Now that the wire default is 0, Xerath opts in explicitly.
            AddParticlePos(_xerath, "xerath_base_q_beam.troy", _startPos, _end,
                lifetime: BeamLifetimeSeconds, overrideTargetHeight: beamElevation,
                bone: "ROOT", targetBone: "TOP", skinColorSourceNetID: _xerath.NetId);

            // Release the cast-recovery lockout (mirrors the disable above; no Rooted to clear).
            _xerath.SetStatus(StatusFlags.CanMove, true);
            _xerath.SetStatus(StatusFlags.CanCast, true);
            _xerath.SetStatus(StatusFlags.CanAttack, true);

            // 3rd arg is `direction` (unit vector along the beam axis), NOT the endpoint.
            // Passing _end directly made TryNormalizeDirection normalize absolute world
            // coords (≈ a fixed NE vector), so the polygon rotated to a constant
            // direction regardless of aim and almost never overlapped real targets.
            var distance = Vector2.Distance(_start, _end);
            var unitsInPolygon = GetUnitsInPolygon(_xerath, _start, _end - _start, 250f, distance,
                new[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-0.5f, 1f) },
                true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectHeroes);
            foreach (var unit in unitsInPolygon)
            {
                var owner = _spell.CastInfo.Owner;
                var ap = owner.Stats.AbilityPower.Total * owner.Spells[0].SpellData.Coefficient;
                var damage = 80 + 40 * (owner.Spells[0].CastInfo.SpellLevel - 1) + ap;
                
                // Keyword=caster: replay shows the tar packets carry KeywordNetID=Xerath (wire default is now 0).
                AddParticleTarget(_xerath, unit, "xerath_base_q_tar.troy", unit, lifetime: 3.0f,
                    skinColorSourceNetID: _xerath.NetId);
                
                unit.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false,
                    _spell);
            }
        }

    }
}