using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class EzrealArcaneShift : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var targetPos = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
            var ownerPos = owner.Position;
            var distance = Vector2.Distance(ownerPos, targetPos);
            var range = 475.0f;

            Vector2 teleportPos;
            if (distance > range)
            {
                var direction = Vector2.Normalize(targetPos - ownerPos);
                teleportPos = ownerPos + direction * range;
            }
            else
            {
                teleportPos = targetPos;
            }

            AddParticle(owner, null, owner.SkinID == 5 ? "Ezreal_arcaneshift_cas_pulsefire" : "ezreal_arcaneshift_cas", owner.Position);
            TeleportTo(owner, teleportPos.X, teleportPos.Y);
            AddParticleTarget(owner, owner, owner.SkinID == 5 ? "Ezreal_arcaneshift_flash_Pulsefire" : "ezreal_arcaneshift_flash", owner);

            float missileRange = 750.0f;
            var target = GetUnitsInRange(owner, teleportPos, missileRange, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectNeutral).FirstOrDefault(unit => unit.IsVisibleByTeam(owner.Team));

            if (target != null)
            {
                SpellCast(owner, 1, SpellSlotType.ExtraSlots, false, target, owner.Position, overrideForceLevel: spell.CastInfo.SpellLevel);
            }
        }
    }

    public class EzrealArcaneShiftMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target,
                ReplicateOnLaunch = true
            },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunch);
        }

        private void OnLaunch(Spell spell, SpellMissile missile)
        {
            var owner = missile.CastInfo.Owner;
            var source = owner.GetSpell("EzrealArcaneShift");
            var rank = missile.CastInfo.SpellLevel;
            var damage = source.SpellData.GetEffectAmount(1, rank)
                + owner.Stats.AbilityPower.Total * source.SpellData.Coefficient;
            EzrealHelpers.OnHit(missile, victim =>
            {
                victim.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, source);
                AddParticle(owner, victim, "Ezreal_arcaneshift_tar", victim.Position, bone: "root", direction: victim.Direction, flags: FXFlags.GivenDirection, keywordObject: owner);
                AddBuff("EzrealRisingSpellForce", 6, 1, source, owner, owner);
                missile.SetToRemove();
            });
        }
    }
}