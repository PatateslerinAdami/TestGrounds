using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
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
        // Per-unit last-damage time (ms) for the 0.5s Crowstorm tick cadence. OnUpdate carries no diff
        // (Riot OnTrigger), so we pace against GameTime() exactly as Riot scripts do. Cleared per cast.
        private readonly Dictionary<uint, float> _lastTickTime = new Dictionary<uint, float>();

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
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

            // Crowstorm damage as an owner-anchored AreaTriggerSphere (replaces SpellSector): while an enemy
            // stands inside, OnUpdate fires per tick; CrowstormTick damages it every 0.5s (old Tickrate 2)
            // paced via GameTime. Radius 600 == old Max(Length,Width). Removed after the 5s lifetime.
            _lastTickTime.Clear();
            int zoneId = CreateAreaTriggerSphereAttached(owner, 600f, onUpdate: u => CrowstormTick(spell, u));
            owner.RegisterTimer(new GameScriptTimer(5.0f, () => DeleteAreaTrigger(zoneId)));
        }
        private void CrowstormTick(Spell spell, AttackableUnit target)
        {
            var owner = spell.CastInfo.Owner;
            if (target == null || !spell.SpellData.IsValidTarget(owner, target) || owner.Team == target.Team)
            {
                return;
            }

            // 0.5s per-unit cadence (old Tickrate 2). OnUpdate fires every server tick; pace via GameTime.
            float now = ApiMapFunctionManager.GameTime();
            if (_lastTickTime.TryGetValue(target.NetId, out var last) && now - last < 500f)
            {
                return;
            }
            _lastTickTime[target.NetId] = now;

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