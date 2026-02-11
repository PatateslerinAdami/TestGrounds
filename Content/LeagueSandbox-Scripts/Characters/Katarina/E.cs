using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeaguePackets.Game.Common.CastInfo;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class KatarinaE : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = false,
            IsDamagingSpell = true,
        };
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            PlayAnimation(owner, "Spell3", 0.3f, flags: AnimationFlags.Override);
            if (target.Team != owner.Team)
            {
                float AP = owner.Stats.AbilityPower.Total * 0.4f;
                float damage = 45f + 25 * spell.CastInfo.SpellLevel + AP;

                target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
                spell.ApplyEffects(target);
                AddParticleTarget(owner, null, "katarina_shadowStep_tar.troy", target);
            }
            AddBuff("KatarinaEReduction", 1.5f, 1, spell, owner, owner);

            var tpLoc = target.Position - new Vector2(target.Direction.X, target.Direction.Z) * target.CollisionRadius;
            TeleportTo(owner, tpLoc.X, tpLoc.Y);
            spell.CastInfo.DesignerCastTime = 0f;
        }

    }
}
