using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
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
        private Spell _spell;
        private Vector2 _endPos2D;
        private Vector2 _castStartPos2D;
        private const float MaxDashRange = 650f;
        private const float JumpToDashDelaySeconds = 0.30f;
        private const float LandingDashDurationS = 0.30f;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _aatrox = owner;
            _spell = spell;

            ApiEventManager.OnUpdateStats.AddListener(this, _aatrox, OnUpdateStats);
            ApiEventManager.OnMoveSuccess.AddListener(this, _aatrox, OnMoveSuccess);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            var healthCost = _aatrox.Stats.CurrentHealth * 0.1f;
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
            if (desiredDistance > MaxDashRange)
            {
                _endPos2D = _castStartPos2D + (Vector2.Normalize(desiredDirection) * MaxDashRange);
            }
        }

        public void OnSpellCast(Spell spell)
        {
            //PlayAnimation(_aatrox, Vector2.Distance(_aatrox.Position, _endPos2D) <= 250f ? "Spell1_Close" : "Spell1", 1f);
            var allyCircleParticle = _aatrox.SkinID switch
            {
                1 => "Aatrox_Skin01_Q_Tar_Green",
                2 => "Aatrox_Skin02_Q_Tar_Green",
                _ => "Aatrox_Base_Q_Tar_Green"
            };
            var enemyCircleParticle = _aatrox.SkinID switch
            {
                1 => "Aatrox_Skin01_Q_Tar_Red",
                2 => "Aatrox_Skin02_Q_Tar_Red",
                _ => "Aatrox_Base_Q_Tar_Red"
            };
            AddParticle(_aatrox, null, allyCircleParticle, _endPos2D, enemyParticle: enemyCircleParticle);
        }

        public void OnSpellPostCast(Spell spell)
        {
            Jump();
        }

        private void Jump()
        {
            FaceDirection(_endPos2D, _aatrox, true);

            Vector2 direction = new Vector2(_aatrox.Direction.X, _aatrox.Direction.Z);
            if (direction == Vector2.Zero) direction = new Vector2(1, 0);

            float jumpDistance = 10f;
            Vector2 jumpTarget = _aatrox.Position;
            Vector2 jumpTargetFal = _aatrox.Position - (direction * jumpDistance);

            var jumpParams = new ForceMovementParameters
            {
                TargetPosition = jumpTarget,
                ParabolicStartPoint = jumpTargetFal,
                Duration = JumpToDashDelaySeconds, 
                ParabolicGravity = 1f,
                PathSpeedOverride = 0.5f,
                IgnoreTerrain = true,
                MovementName = "AatroxQJump",
                Animation = "Spell1",
                OverrideRunAnimation = false,
                AnimationFlags = AnimationFlags.UniqueOverride | AnimationFlags.Unknown6,
                AnimationTimeScale = 0f,
                AnimationStartTime = 0f,
                AnimationSpeedScale = 1f,
                SetStatus = StatusFlags.CanMove | StatusFlags.CanCast | StatusFlags.CanAttack
            };

            _aatrox.StartForcedMovement(jumpParams);
        }

        private void Dash()
        {
            FaceDirection(_endPos2D, _aatrox, true);
            var desiredDirection = _endPos2D - _castStartPos2D;
            var desiredDistance = desiredDirection.Length();
            var dashTarget = desiredDistance > MaxDashRange
                ? _castStartPos2D + (Vector2.Normalize(desiredDirection) * MaxDashRange)
                : _endPos2D;

            var distance = (dashTarget - _aatrox.Position).Length();
            if (distance <= 1f)
            {
                OnMoveSuccess(_aatrox, new ForceMovementParameters { MovementName = "AatroxQDash" });
                return;
            }

            var dashParams = new ForceMovementParameters
            {
                TargetPosition = dashTarget,
                Duration = LandingDashDurationS, 
                ParabolicGravity = 0f,
                MovementName = "AatroxQDash",
                SetStatus = StatusFlags.CanMove | StatusFlags.CanCast | StatusFlags.CanAttack
            };

            _aatrox.StartForcedMovement(dashParams);
        }

        public void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters.MovementName == "AatroxQJump")
            {
                Dash();
            }
            else if (parameters.MovementName == "AatroxQDash")
            {
                StopAnimation(_aatrox, "Spell1", fade: true);
                StopAnimation(_aatrox, "Spell1_Close", fade: true);

                var landParticle = _aatrox.SkinID switch
                {
                    1 => "Aatrox_Skin01_Q_Land",
                    2 => "Aatrox_Skin02_Q_Land",
                    _ => "Aatrox_Base_Q_Land"
                };
                AddParticle(_aatrox, null, landParticle, _aatrox.Position);

                var enemiesInKnockUpRange = GetUnitsInRange(_aatrox, _aatrox.Position, 150f, true,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                    SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
                foreach (var enemy in enemiesInKnockUpRange.Where(u => _spell.SpellData.IsValidTarget(_aatrox, u)))
                {
                    AddBuff("AatroxQ", 1f, 1, _spell, enemy, _aatrox);
                }

                var enemiesInRange = GetUnitsInRange(_aatrox, _aatrox.Position, 300f, true,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                    SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
                var ad = _aatrox.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
                var dmg = 70f + 45f * (_spell.CastInfo.SpellLevel - 1) + ad;

                var hitParticle = _aatrox.SkinID switch
                {
                    1 => "Aatrox_Skin01_Q_Hit",
                    2 => "Aatrox_Skin02_Q_Hit",
                    _ => "Aatrox_Base_Q_Hit"
                };
                foreach (var enemy in enemiesInRange)
                {
                    AddParticleTarget(_aatrox, enemy, hitParticle, enemy);
                    enemy.TakeDamage(_aatrox, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                        DamageResultType.RESULT_NORMAL);
                }
            }
        }

        private void OnUpdateStats(AttackableUnit unit, float diff)
        {
            var cost = _aatrox.Stats.CurrentHealth * 0.1f;
            SetSpellToolTipVar(_aatrox, 2, cost, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
        }
    }
}
