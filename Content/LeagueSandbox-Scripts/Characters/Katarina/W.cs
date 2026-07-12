using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class KatarinaW : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            IsDamagingSpell = true,
            TriggersSpellCasts = true
        };
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit, false);
        }

        // No announce fill here: the engine's FillCastTargetsFromSpellShape handles it (SelfAOE,
        // CastRadius 400) with the spell's DATA flags — which for KatarinaW lack AffectEnemies
        // (Riot's own 4.20 inibin: Neutral|Minions|Heroes). Replay-verified that this is exactly
        // what Riot announces: every populated W ANS target resolved to a JUNGLE MONSTER
        // (Razorbeak/Scuttle/Red-camp NetIDs), never an enemy champion — a manual
        // AddCastTargetsInRange with AffectEnemies announced units Riot never does.
        // Announce != damage: the damage loop below keeps its own explicit flags.

        public void OnSpellCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            // Replay byte was 0x80 = pure junk bit 7; effective flags are None.
            PlayAnimation(owner, "Spell2", 0.5f, flags: AnimationFlags.None);
            AddParticleTarget(owner, owner, "katarina_w_cas.troy", owner, bone: "C_BUFFBONE_GLB_CENTER_LOC");
            
            // Sinister Steel = instant self-centered AoE burst. Resolve the hit-set inline (Riot Category-1
            // area query) instead of a SpellSector, then route EACH hit through spell.ApplyEffects — the
            // single hit-application chokepoint that fires OnSpellHit/OnBeingSpellHit. That keeps both the
            // W damage (OnSpellHit, an OnSpellHit listener) AND cross-script reactors (KatarinaQMark
            // dagger detonation, which listens on KatarinaW's OnSpellHit) firing, exactly as the old
            // sector's HitUnit->ApplyEffects did. Raw TakeDamage here would silently drop those listeners.
            // Radius 400 == old SectorParameters.Length; centered on Katarina (self-AoE).
            var flags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes;
            foreach (var target in GetUnitsInRange(owner, owner.Position, 400f, true, flags))
            {
                spell.ApplyEffects(target);
            }
        }
        private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile swag)
        {
            var owner = spell.CastInfo.Owner;
            var AP = spell.CastInfo.Owner.Stats.AbilityPower.Total * 0.25f;
            var AD = spell.CastInfo.Owner.Stats.AttackDamage.Total * 0.6f;
            float damage = 5f + spell.CastInfo.SpellLevel * 35f + AP + AD;

            target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false, spell);
            AddParticleTarget(owner, target, "katarina_w_tar.troy", target, 1f);
            if (target is Champion) AddBuff("KatarinaWHaste", 1f, 1, spell, owner, owner);
        }
    }
}
