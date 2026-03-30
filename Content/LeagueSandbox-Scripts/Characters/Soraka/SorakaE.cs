using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using System.Runtime.InteropServices;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class SorakaE : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            IsDamagingSpell = true,
            TriggersSpellCasts = true
        };
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellPostCast.AddListener(this, spell, OnSpellPostCast, false);

        }

        Particle p1;
        Particle beam;
        private SpellSector EZone;
        private SpellSector EZoneEnd;

        Spell spell;

        public void OnSpellPostCast(Spell spell)
        {
            this.spell = spell;
            var owner = spell.CastInfo.Owner;
            var Cursor = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);

            p1 = AddParticle(owner, null, "Soraka_Base_E_tar.troy", Cursor, 1.5f);
            beam = AddParticle(owner, owner, "Soraka_Base_E_Beam.troy", Cursor, 1.5f); // TODO: make the beam shoot from her hand
            AddParticle(owner, null, "Soraka_Base_E_rune.troy", Cursor, 1.5f);

            EZone = spell.CreateSpellSector(new SectorParameters
            {
                Width = 260,
                Length = 260,
                SingleTick = true,
                Lifetime = 1.5f,
                Tickrate = 30,
                CanHitSameTarget = false,
                Type = SectorType.Area,
                OverrideFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.IgnoreLaneMinion | SpellDataFlags.IgnoreEnemyMinion | SpellDataFlags.AffectHeroes
            });
            ApiEventManager.OnSpellSectorHit.AddListener(this, EZone, EZoneHit, false);

            owner.RegisterTimer(new GameScriptTimer(1.5f, () =>
            {
                EZoneEnd = spell.CreateSpellSector(new SectorParameters
                {
                    Width = 260,
                    Length = 260,
                    SingleTick = true,
                    Lifetime = 0.1f,
                    Tickrate = 30,
                    CanHitSameTarget = false,
                    Type = SectorType.Area,
                    OverrideFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.IgnoreLaneMinion | SpellDataFlags.IgnoreEnemyMinion | SpellDataFlags.AffectHeroes
                });
                ApiEventManager.OnSpellSectorHit.AddListener(this, EZoneEnd, EZoneEndHit, false);
            }));
        }
        public void EZoneHit(SpellSector sector, AttackableUnit target)
        {
            var owner = spell.CastInfo.Owner;
            var AP = spell.CastInfo.Owner.Stats.AbilityPower.Total * 0.40f;
            float damage = 70f + spell.CastInfo.SpellLevel * 40f + AP;

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, spell); // TODO: fix the damage, it's too high
            AddParticleTarget(owner, target, "soraka_base_e_enemy_tar.troy", target, 1f);
            if (target is Champion && target.Team != owner.Team)
            {
                AddBuff("SorakaEPacify", 1f, 1, spell, target, owner);
            }
        }

        public void EZoneEndHit(SpellSector sector, AttackableUnit target)
        {
            var owner = spell.CastInfo.Owner;
            var AP = spell.CastInfo.Owner.Stats.AbilityPower.Total * 0.40f;
            float damage = 70f + spell.CastInfo.SpellLevel * 40f + AP;

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, spell); // TODO: fix the damage, it's too high
            if (target is Champion && target.Team != owner.Team)
            {
                AddBuff("SorakaESnare", 1.5f, 1, spell, target, owner);
            }
        }
    }
}
