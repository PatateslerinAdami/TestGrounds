using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class KatarinaQ : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            MissileParameters = new MissileParameters
            {
                Type = MissileType.Chained,
                MaximumHits = 5,
                CanHitSameTarget = false,
                BounceSpellName = "KatarinaQMis"
            },
            IsDamagingSpell = true,
            TriggersSpellCasts = true,
            
        };
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        }

        public void OnSpellCast(Spell spell)
        {
        }
        public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
        {
            var owner = spell.CastInfo.Owner;
            if (target.HasBuff("KatarinaQMark"))
            {
                float markBase = 15 * spell.CastInfo.SpellLevel;
                float markAp = 0.15f;
                float markDmg = markBase + (owner.Stats.AbilityPower.Total * markAp);

                target.TakeDamage(owner, markDmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
                AddParticleTarget(owner, target, "katarina_bouncingBlades_tar.troy", target, 1.0f);
            }

            float baseDamage = 60 + (25 * (spell.CastInfo.SpellLevel - 1));
            float apRatio = 0.45f;
            float damage = baseDamage + (owner.Stats.AbilityPower.Total * apRatio);

            // Wanted to see if we could reduce the damage on subsequent hit
            if (missile is SpellChainMissile chainMissile)
            {
                int bounceIndex = chainMissile.HitCount - 1;

                float reduction = 1.0f - (0.10f * bounceIndex);

                if (reduction < 0) reduction = 0;

                damage *= reduction;
            }
            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);

            AddBuff("KatarinaQMark", 4.0f, 1, spell, target, owner);
            AddParticleTarget(owner, target, "Katarina_Base_Q_Tar.troy", target, lifetime: 1.0f);
        }
    }

    public class KatarinaQMis : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
        };
    }
}
