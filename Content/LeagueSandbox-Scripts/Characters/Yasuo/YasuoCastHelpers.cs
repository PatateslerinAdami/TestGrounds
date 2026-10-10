using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using GameServerCore.Enums;
using System;
using System.Numerics;
using GameServerLib.GameObjects.AttackableUnits;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    internal static class YasuoCastHelpers
    {
        internal static void ResetSheath(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var buff = owner.GetBuffWithName("YasuoSwordOut");
            if (buff != null && !buff.Remove && buff.BuffScript is Buffs.YasuoSwordOut sword)
            {
                buff.Refresh();
                sword.Reset();
            }
            else
            {
                AddBuff("YasuoSwordOut", Buffs.YasuoSwordOut.IdleDelay + Buffs.YasuoSwordOut.TransitionTime, 1, spell, owner, owner);
            }
        }

        internal static void SyncQRank(ObjAIBase owner, Spell wrapper)
        {
            StopComboAnimation(owner);
            wrapper.CastInfo.Cooldown = wrapper.SpellData.Cooldown[wrapper.CastInfo.SpellLevel];
            RefreshQTooltip(wrapper);
            string[] names = { "YasuoQ", "YasuoQ2", "YasuoQ3", "YasuoQ3Mis" };
            for (int i = 0; i < names.Length; i++)
            {
                var extra = owner.GetSpell(names[i]) ?? SetSpell(owner, names[i], SpellSlotType.ExtraSlots, i, true);
                extra.CastInfo.SpellLevel = wrapper.CastInfo.SpellLevel;
            }
        }

        private static Buffs.YasuoDashController CurrentDash(ObjAIBase owner)
        {
            return owner.Spells.TryGetValue(2, out var e) && e.Script is YasuoDashWrapper script ? script.Dash : null;
        }

        internal static Buffs.YasuoDashController ActiveDash(ObjAIBase owner)
        {
            var dash = CurrentDash(owner);
            return dash?.IsActive == true ? dash : null;
        }

        internal static void StopComboAnimation(ObjAIBase owner)
        {
            CurrentDash(owner)?.StopComboAnimation();
        }

        internal static bool TryArmCombo(Spell wrapper)
        {
            var dash = ActiveDash(wrapper.CastInfo.Owner);
            if (dash == null)
            {
                return false;
            }

            dash.ArmCombo(wrapper);
            return true;
        }

        internal static void PrepareThrust(ObjAIBase owner, Spell spell)
        {
            float bonus = BonusAttackSpeed(owner);
            float minimum = spell.SpellName == "YasuoQ3" ? 0.28f : 0.175f;
            spell.CastInfo.DesignerCastTime = MathF.Max(minimum, 0.35f * (1 - MathF.Min(bonus, 1.2f) / 2.4f));
            spell.CastInfo.IsOverrideCastPosition = true;
            spell.CastInfo.Targets.Clear();
            spell.CastInfo.AddTarget(owner);
            var position = owner.GetPosition3D();
            var direction = new Vector3(owner.Direction.X, 0, owner.Direction.Z);
            if (direction.LengthSquared() < 0.001f)
            {
                direction = Vector3.UnitX;
            }

            direction = Vector3.Normalize(direction);
            spell.CastInfo.SpellCastLaunchPosition = position - direction * 70;
            spell.CastInfo.TargetPosition = spell.CastInfo.SpellCastLaunchPosition;
            spell.CastInfo.TargetPositionEnd = position;
        }

        private static float BonusAttackSpeed(ObjAIBase owner)
        {
            var speed = owner.Stats.AttackSpeedMultiplier;
            return MathF.Max(0, (speed.BaseValue + speed.BaseBonus) * (1 + speed.PercentBaseBonus) + speed.FlatBonus - 1);
        }

        internal static Vector3 ThrustDirection(Spell spell)
        {
            var direction = spell.CastInfo.TargetPositionEnd - spell.CastInfo.SpellCastLaunchPosition;
            direction.Y = 0;
            return direction.LengthSquared() > 0.001f ? Vector3.Normalize(direction) : Vector3.UnitX;
        }

        internal static void AddThrustVisuals(Spell spell)
        {
            AddBuff("YasuoThrustVisuals", 0.5f, 1, spell, spell.CastInfo.Owner, spell.CastInfo.Owner);
        }

        internal static float QCooldown(Spell spell, int? rank = null)
        {
            int level = Math.Clamp(rank ?? spell.CastInfo.SpellLevel, 1, 5);
            float baseTime = spell.SpellData.GetEffectAmount(4, level);
            return baseTime * MathF.Max(0.3334f, 1 - 0.6f * BonusAttackSpeed(spell.CastInfo.Owner));
        }

        internal static void RefreshQTooltip(Spell spell)
        {
            if (spell == null || spell.SpellName is not ("YasuoQW" or "YasuoQ2W" or "YasuoQ3W"))
            {
                return;
            }

            if (spell.CastInfo.SpellLevel < 5)
            {
                spell.SetToolTipVar(0, QCooldown(spell, spell.CastInfo.SpellLevel + 1)); // f1: next rank
            }

            spell.SetToolTipVar(1, QCooldown(spell)); // f2: current rank
        }

        internal static float QDamage(Spell spell, byte? rank = null)
        {
            return 20 * (rank ?? spell.CastInfo.SpellLevel) + spell.CastInfo.Owner.Stats.AttackDamage.Total;
        }

        internal static void ApplyQDamage(Spell spell, AttackableUnit target, bool firstTarget, byte? rank = null)
        {
            var owner = spell.CastInfo.Owner;
            bool critical = Random.Shared.NextDouble() < Math.Clamp(owner.Stats.CriticalChance.Total, 0, 1);
            float amount = QDamage(spell, rank);
            if (critical)
            {
                amount += owner.Stats.AttackDamage.Total * (owner.Stats.CriticalDamage.Total * (0.75f / 0.9f) - 1);
            }

            var hit = new DamageData
            {
                Attacker = owner,
                Target = target,
                Damage = amount,
                PostMitigationDamage = target.Stats.GetPostMitigationDamage(amount, DamageType.DAMAGE_TYPE_PHYSICAL, owner),
                DamageType = DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource = firstTarget ? DamageSource.DAMAGE_SOURCE_ATTACK : DamageSource.DAMAGE_SOURCE_SPELLAOE,
                AppliesOnHitEffects = firstTarget,
                UsesLifeSteal = firstTarget,
                IsAutoAttack = false,
                DamageResultType = critical ? DamageResultType.RESULT_CRITICAL : DamageResultType.RESULT_NORMAL
            };
            target.TakeDamage(hit, critical, spell);
        }

        internal const float KnockupDuration = 0.75f;

        internal static string KnockupBuffName(AttackableUnit target)
        {
            return target is Minion && target is not Pet { IsClone: true } ? "YasuoQ3MisMinion" : "YasuoQ3Mis";
        }
    }
}
