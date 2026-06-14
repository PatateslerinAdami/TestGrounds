using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class AatroxQ : ISpellScript
    {
        private ObjAIBase _aatrox;
        private       Spell          _spell;
        private       Vector2        _endPos2D;
        private       Vector2        _castStartPos2D;
        private const float          MaxDashRange         = 650f;
        // Ascend/dive timings are replay-measured (663eda09, 52/52 Q-casts): ascend = ~24.4u hop at
        // ~25.8 u/s (≈0.95s flight) with ParabolicGravity=10; dive starts ~0.40s after the ascend and
        // lasts a fixed ~0.085s (speed scales with distance: 88u→1034, 253u→3058).
        private const float          AscendDistance        = 24.4f;
        private const float          AscendDurationSeconds = 0.95f;
        private const float          JumpToDashDelaySeconds = 0.40f;
        private const float          LandingDashDurationS  = 0.085f;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _aatrox = owner;
            _spell = spell;
            
            ApiEventManager.OnUpdateStats.AddListener(this, _aatrox, OnUpdateStats);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var healthCost                  = _aatrox.Stats.CurrentHealth * 0.1f;
            _aatrox.Stats.CurrentHealth = Math.Max(1, _aatrox.Stats.CurrentHealth - healthCost);
            var buff = _aatrox.GetBuffWithName("AatroxPassive")?.BuffScript as AatroxPassive;
            buff?.AddBlood(healthCost);
            
            _spell = spell;
            
            _castStartPos2D = owner.Position;
            _endPos2D = end != Vector2.Zero
                ? end
                : start != Vector2.Zero
                    ? start
                    : new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);

            var desiredDirection = _endPos2D - _castStartPos2D;
            var desiredDistance = desiredDirection.Length();
            if (desiredDistance > MaxDashRange) {
                _endPos2D = _castStartPos2D + (Vector2.Normalize(desiredDirection) * MaxDashRange);
            }
        }

        public void OnSpellCast(Spell spell)
        {
            PlayAnimation(_aatrox, Vector2.Distance(_aatrox.Position, _endPos2D) <= 250f ? "Spell1_Close" : "Spell1", 1f);
            var allyCircleParticle = _aatrox.SkinID switch {
                1 => "Aatrox_Skin01_Q_Tar_Green",
                2 => "Aatrox_Skin02_Q_Tar_Green",
                _ => "Aatrox_Base_Q_Tar_Green"
            };
            var enemyCircleParticle = _aatrox.SkinID switch {
                1 => "Aatrox_Skin01_Q_Tar_Red",
                2 => "Aatrox_Skin02_Q_Tar_Red",
                _ => "Aatrox_Base_Q_Tar_Red"
            };
            AddParticle(_aatrox, null, allyCircleParticle, _endPos2D, enemyParticle: enemyCircleParticle);
        }

        public void OnSpellPostCast(Spell spell)
        {
            Jump();
            _aatrox.RegisterTimer(new GameScriptTimer(JumpToDashDelaySeconds, () =>
            {
                Dive();
            }));
        }

        private void Jump()
        {
            _aatrox.StopMovement(networked: false);
            FaceDirection(_endPos2D, _aatrox, true);
            Vector2 direction = new Vector2(_aatrox.Direction.X, _aatrox.Direction.Z);

            float jumpDistance = AscendDistance;
            Vector2 jumpTarget = _aatrox.Position + (direction * jumpDistance);

            // Ascend = a real (near-in-place) force-move arc over the 0x64 dash wire — replay-verified
            // ParabolicGravity=10 and speed ~25 u/s on all 52 Q-casts. Replaces the old CustomDashTest
            // custom packet (which bypassed the force-move state and needed manual SetStatus);
            // lockActions (default) now owns the move/cast lockout. The dive (Dive(), JumpToDashDelay
            // later) ends this ascend via its own force-move and takes over.
            // NOTE: speed drives the flight TIME (= jumpDistance/speed), and the parabola apex scales
            // with flightTime² — so a too-low speed (the old jumpDistance/5 = 2 u/s → ~5s flight) shot
            // Aatrox hugely upward in the first frames. ~0.4s flight (speed ~25) is the replay's small hop.
            float jumpSpeed = jumpDistance / AscendDurationSeconds;
            Dash(_aatrox, jumpTarget, jumpSpeed, gravity: 10f, keepFacing: true, movementName: "AatroxQAscend");
        }
        private void Dive()
        {
            FaceDirection(_endPos2D, _aatrox, true);
            var desiredDirection = _endPos2D - _castStartPos2D;
            var desiredDistance = desiredDirection.Length();
            var dashTarget = desiredDistance > MaxDashRange
                ? _castStartPos2D + (Vector2.Normalize(desiredDirection) * MaxDashRange)
                : _endPos2D;

            var distance = (dashTarget - _aatrox.Position).Length();
            if (distance <= 1f) {
                return;
            }
            var speed = distance / LandingDashDurationS;
            Dash(_aatrox, _endPos2D, speed, movementName: "AatroxQDash");

            ApiEventManager.OnMoveSuccess.AddListener(this, _aatrox, OnMoveSuccess, true);

        }
        public void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters.MovementName != "AatroxQDash") return;

            StopAnimation(_aatrox, "Spell1", StopAnimationFlags.Fade | StopAnimationFlags.IgnoreLock);
            StopAnimation(_aatrox, "Spell1_CLose", StopAnimationFlags.Fade | StopAnimationFlags.IgnoreLock);
            
            var landParticle = _aatrox.SkinID switch {
                1 => "Aatrox_Skin01_Q_Land",
                2 => "Aatrox_Skin02_Q_Land",
                _ => "Aatrox_Base_Q_Land"
            };
            AddParticle(_aatrox, null, "Aatrox_Base_Q_Land", _aatrox.Position);

            var enemiesInKnockUpRange = GetUnitsInRange(_aatrox, _aatrox.Position, 150f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
            foreach (var enemy in enemiesInKnockUpRange.Where(u => _spell.SpellData.IsValidTarget(_aatrox, u))) {
                AddBuff("AatroxQ", 1f, 1, _spell, enemy, _aatrox);
            }
            
            var enemiesInRange = GetUnitsInRange(_aatrox, _aatrox.Position, 300f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
            var ad  = _aatrox.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
            var dmg = 70f + 45f * (_spell.CastInfo.SpellLevel - 1) + ad;
            
            var hitParticle = _aatrox.SkinID switch {
                1 => "Aatrox_Skin01_Q_Hit",
                2 => "Aatrox_Skin02_Q_Hit",
                _ => "Aatrox_Base_Q_Hit"
            };
            foreach (var enemy in enemiesInRange){
                AddParticleTarget(_aatrox, enemy, hitParticle, enemy);
                enemy.TakeDamage(_aatrox, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                    DamageResultType.RESULT_NORMAL);
            }
        }
        
        private void OnUpdateStats(AttackableUnit unit, float diff) {
            var cost = _aatrox.Stats.CurrentHealth * 0.1f;
            SetSpellToolTipVar(_aatrox, 2, cost, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
        }
    }
}
