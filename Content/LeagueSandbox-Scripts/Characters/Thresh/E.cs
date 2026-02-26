using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class ThreshE : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true,
            IsDamagingSpell = true,
            NotSingleTargetSpell = true,
            AutoFaceDirection = true
        };
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            Vector2 dragStart = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
            Vector2 dragEnd = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);
            Vector2 direction = dragEnd - dragStart;

            if (direction.LengthSquared() > 0)
            {
                direction = Vector2.Normalize(direction);
            }
            else
            {
                direction = new Vector2(owner.Direction.X, owner.Direction.Z);
            }

            Vector2 facing = new Vector2(owner.Direction.X, owner.Direction.Z);
            float castAngle = (float)Math.Atan2(direction.Y, direction.X);
            float facingAngle = (float)Math.Atan2(facing.Y, facing.X);

            float angle = (castAngle - facingAngle) * (180f / (float)Math.PI);

            while (angle > 180f) angle -= 360f;
            while (angle <= -180f) angle += 360f;
            //makes me believe there is something wrong with the spell system.
            string animName;
            if (angle > -45f && angle <= 45f)
                animName = "spell3_p0";        
            else if (angle > 45f && angle <= 135f)
                animName = "Spell3_pL90";     
            else if (angle > -135f && angle <= -45f)
                animName = "Spell3_p90";       
            else if (angle > 135f)
                animName = "spell3_pL180";     
            else
                animName = "Spell3_p180";       

            OverrideAnimation(owner, animName, "Spell3");
        }
        public void OnSpellCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;

            Vector2 dragStart = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
            Vector2 dragEnd = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);
            Vector2 direction = dragEnd - dragStart;

            if (direction.LengthSquared() > 0)
            {
                direction = Vector2.Normalize(direction);
            }
            else
            {
                direction = new Vector2(owner.Direction.X, owner.Direction.Z);
            }

            Vector2 missileStart = owner.Position - (direction * 500f);
            Vector2 missileEnd = owner.Position + (direction * 500f);
            SpellCast(owner, 5, SpellSlotType.ExtraSlots, missileStart, missileEnd, true, missileStart);

        }

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            owner.RegisterTimer(new GameScriptTimer(1.4f, () =>
            {
                //ClearOverrideAnimation(owner, "Spell3");
            }));
            
        }
    }

    public class ThreshEMissile1 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Circle
            },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = missile.CastInfo.Owner;
            var threshESpell = owner.GetSpell("ThreshE");
            if (threshESpell == null) return;

            int spellLevel = threshESpell.CastInfo.SpellLevel;
            if (spellLevel == 0) return;

            float[] baseDamage = { 65f, 95f, 125f, 155f, 185f };
            float damage = baseDamage[spellLevel - 1] + (owner.Stats.AbilityPower.Total * 0.4f);

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);


            Vector2 pushDir = new Vector2(missile.Direction.X, missile.Direction.Z);
            float horizontalDistance = 200f;
            Vector2 pushEnd = target.Position + (pushDir * horizontalDistance);
            float desiredDuration = 0.75f;//1.2f
            float desiredHeight = 5.0f;
            float requiredGravity = desiredHeight / (desiredDuration * desiredDuration);
            float requiredSpeed = horizontalDistance / desiredDuration;
            ForceMovement(target, "run", pushEnd, requiredSpeed, 0f, requiredGravity, 0f, true, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersType.POSTPONE_CURRENT_ORDER, ForceMovementOrdersFacing.KEEP_CURRENT_FACING);
            AddBuff("ThreshEStun", 1.0f, 1, threshESpell, target, owner);
            AddParticleTarget(owner, target, "Thresh_E_hit.troy", target, 1.0f);
        }
    }
}

namespace Buffs
{
    public class ThreshEStun : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SLOW,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        Particle p;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            int spellLevel = ownerSpell.CastInfo.SpellLevel;

            float[] slowPercents = { -0.20f, -0.25f, -0.30f, -0.35f, -0.40f };
            float slowAmount = slowPercents[spellLevel - 1];

            StatsModifier.MoveSpeed.PercentBonus = slowAmount;
            unit.AddStatModifier(StatsModifier);

            p = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "Global_Slow.troy", unit, buff.Duration);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            p.SetToRemove();
        }
    }
}