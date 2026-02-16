using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class Crowstorm : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = true,
            ChannelDuration = 1.5f,
        };
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }
        public void OnSpellPostChannel(Spell spell)
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
            TeleportTo(owner, teleportPos.X, teleportPos.Y);

            AddParticle(owner, owner, "crowstorm_green_cas.troy", default, 5f, teamOnly: owner.Team);
            AddParticle(owner, owner, "crowstorm_red_cas.troy", default, 5f, teamOnly: CustomConvert.GetEnemyTeam(owner.Team));
            AddBuff("Crowstorm", 5f, 1, spell, owner, owner);

            spell.CreateSpellSector(new SectorParameters
            {
                Length = 600f, 
                Tickrate = 2, 
                CanHitSameTarget = true,
                CanHitSameTargetConsecutively = true,
                Type = SectorType.Area,
                Lifetime = 5.0f,
                BindObject = owner 
            });
        }
        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;

            float baseDamage = 125 + (100 * (spell.CastInfo.SpellLevel - 1));
            float apDamage = 0.45f * owner.Stats.AbilityPower.Total;
            float damagePerTick = (baseDamage + apDamage) / 2.0f;

            target.TakeDamage(owner, damagePerTick, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, spell);
        }
        public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
        {
            var owner = spell.CastInfo.Owner;
            if (owner.HasBuff("Crowstorm"))
            {
                RemoveBuff(owner, "Crowstorm");
            }
        }
    }
}