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
            var owner = spell.CastInfo.Owner as Champion;
            if (owner == null)
            {
                return;
            }
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
            AddParticle(owner, null, "ezreal_arcaneshift_cas", owner.Position);
            TeleportTo(owner, teleportPos.X, teleportPos.Y);
            AddParticleTarget(owner, owner, "ezreal_arcaneshift_flash", owner);

            float missileRange = 750.0f;
            var units = GetUnitsInRange(teleportPos, missileRange, true);

            var target = units
                .Where(u => u.Team != owner.Team &&
                            u is ObjAIBase &&
                            !u.IsDead &&
                            u.Status.HasFlag(StatusFlags.Targetable) &&
                            u.IsVisibleByTeam(owner.Team))
                .OrderBy(u => Vector2.DistanceSquared(teleportPos, u.Position))
                .FirstOrDefault();

            if (target != null)
            {
                SpellCast(owner, 1, SpellSlotType.ExtraSlots, false, target, owner.Position);
            }
        }
    }

    public class EzrealArcaneShiftMissile : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Target
            },
            IsDamagingSpell = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;

            var mainSpell = owner.GetSpell("EzrealArcaneShift");
            int spellLevel = mainSpell != null ? mainSpell.CastInfo.SpellLevel : 0;

            float baseDamage = 75 + (spellLevel * 50);
            float bonusAd = owner.Stats.AttackDamage.BaseBonus * 0.5f;
            float ap = owner.Stats.AbilityPower.Total * 0.75f;

            float totalDamage = baseDamage + bonusAd + ap;
            target.TakeDamage(owner, totalDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
            AddBuff("EzrealRisingSpellForce", 5f, 1, spell, owner, owner);
            missile.SetToRemove();
        }
    }
}