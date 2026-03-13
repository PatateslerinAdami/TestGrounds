using GameMaths;
using GameServerCore;
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

        ObjAIBase _owner;
        Minion _toLookAt;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _toLookAt = AddMinion(_owner, "testcuberender10vision", "testcuberender10vision", owner.Position, owner.Team, ignoreCollision: true, targetable: false, useSpells: false);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            Vector2 targetPosEnd = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);
            Vector2 direction = targetPosEnd - owner.Position;

            if (direction.LengthSquared() > 0)
            {
                direction = Vector2.Normalize(direction);
            }
            else
            {
                direction = new Vector2(owner.Direction.X, owner.Direction.Z);
            }

            var endPos = _owner.Position + direction * 400f;
            _toLookAt.SetPosition(endPos);
            UnitSetLookAt(_owner, _toLookAt, AttackType.ATTACK_TYPE_MELEE);
        }

        public void OnSpellCast(Spell spell)
        {
            Vector2 targetPosEnd = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);
            Vector2 direction = targetPosEnd - _owner.Position;

            if (direction.LengthSquared() > 0)
            {
                direction = Vector2.Normalize(direction);
            }
            else
            {
                direction = new Vector2(_owner.Direction.X, _owner.Direction.Z);
            }

            Vector2 missileStart = _owner.Position - (direction * 500f);
            Vector2 missileEnd = _owner.Position + (direction * 500f);
            SpellCast(_owner, 5, SpellSlotType.ExtraSlots, missileStart, missileEnd, true, missileStart);

            var dir3D = new Vector3(direction.X, 0, direction.Y);
            AddParticlePos(_owner, "Thresh_E_Warn_green.troy", _owner.Position, missileEnd, teamOnly: _owner.Team, direction: dir3D);
            AddParticlePos(_owner, "Thresh_E_Warn_red.troy", _owner.Position, default, teamOnly: CustomConvert.GetEnemyTeam(_owner.Team), ignoreCasterVisibility: true, direction:dir3D);
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