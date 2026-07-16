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
        private ObjAIBase _fiddlesticks;

        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = true,
            ChannelDuration = 1.5f,
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _fiddlesticks = owner;
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        }

        private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
        {
            var ap = _fiddlesticks.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
            var dmg = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;
            var damagePerTick = dmg / 2.0f;

            target.TakeDamage(_fiddlesticks, damagePerTick, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, spell);
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
            TeleportToPosition(owner, teleportPos.X, teleportPos.Y);
            AddBuff("Crowstorm", 5f, 1, spell, owner, owner);
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