using System;
using System.Numerics;
using System.Linq;
using GameServerCore.Enums;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Buffs
{
    internal class TT_RelicAura : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            // The relic is consumed by DYING (see OnUpdate): the neutral camp then respawns a fresh unit.
            // So the buff must NOT persist through death — the death has to remove it, which fires
            // OnDeactivate to clean up the rune FX + the render anchor.
            PersistsThroughDeath = false,
        };

        public StatsModifier StatsModifier { get; private set; }

        // The relic is a NEUTRAL CAMP minion (S1 Map10 NeutralMinionSpawn.lua: CAMPTYPE_RELIC, camp 7,
        // GroupsRespawnTime/GroupDelaySpawnTime). Replay-verified (1255f38c…): on pickup Riot DESTROYS the
        // relic and the camp respawns a brand-new unit — each cycle has a fresh netid (0x400004bc →
        // 0x400009cf → 0x40000e82), each with its own EnterVis + BuffAdd2. So consume == kill the unit;
        // the existing healthPack camp (NeutralMinionSpawn.cs) drives the ~90s respawn. No persistent unit,
        // no internal cooldown timer.
        private const float PROXIMITY_INTERVAL = 250f;

        Particle buffParticle;
        Particle buffParticle2;
        AttackableUnit Unit;
        Minion InvisibleMinion;
        float proximityTimer = PROXIMITY_INTERVAL;
        bool consumed;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            Unit = unit;
            consumed = false;

            buff.SetStatusEffect(StatusFlags.Targetable, false);
            // NOTE: deliberately NOT Invulnerable. The relic is consumed by killing it (OnUpdate), but
            // TakeDamage is gated by CanTakeDamage which refuses while Invulnerable (AttackableUnit.cs:930)
            // — so an invulnerable relic could never die and the camp could never respawn it. Protection
            // comes from NonTargetableAll below (blocks all targeting + AoE; verified faithful), which makes
            // invulnerability redundant anyway.
            buff.SetStatusEffect(StatusFlags.ForceRenderParticles, true);
            // The relic model must NOT render (confirmed from 4.20 footage — only the ground rune is shown).
            // The rune FX below carry the whole visual; ForceRenderParticles keeps them rendering despite NoRender.
            buff.SetStatusEffect(StatusFlags.NoRender, true);
            // The global Targetable flag is recomputed from buffs each tick, but the PER-TEAM targetability is
            // a persistent stat — it's what GetIsTargetableToTeam ultimately gates on (and what Spell.ApplyEffects
            // / acquisition check). Set NonTargetableAll so AoE spells (e.g. Annie W stun) and all targeting skip
            // the relic. (Default is TargetableToAll.)
            unit.Stats.IsTargetableToTeam = SpellDataFlags.NonTargetableAll;

            // The render anchor must not be attackable / auto-targeted (attack-move, auto-attack, Ahri W).
            // targetable:false clears the global Targetable status flag, BUT acquisition (IsTargetableByUnit)
            // checks only the PER-TEAM targetability (IsTargetableToTeam), which AddMinion sets from
            // targetingFlags (default 0 = targetable to all). So we must also pass NonTargetableAll, or the
            // anchor stays acquirable despite the global flag.
            InvisibleMinion = AddMinion(null, "TestCubeRender", "HiddenMinion", unit.Position,
                ignoreCollision: true, targetable: false, targetingFlags: SpellDataFlags.NonTargetableAll);
            // The rune binds to this render anchor (wire: bind = TestCubeRender, bone "bottom"). A bound
            // particle only renders while its bound unit is in the client's vision, and our engine does not
            // reliably re-send a fog-gated unit + its bound FX to a player who gains vision later — so the
            // anchor is made always-visible (model is invisible anyway) so the rune always renders. The relic
            // gameplay unit stays fog-gated/untargetable.
            InvisibleMinion.AlwaysVisible = true;
            // Same reason as the relic above: NOT Invulnerable, or the anchor's own TakeDamage cleanup
            // would be refused by CanTakeDamage and the anchor (+ its bound rune FX) would never die.
            // It's NonTargetableAll, so nothing can damage it anyway.

            if (unit is ObjAIBase obj)
            {
                AddBuff("ResistantSkinDragon", 25000f, 1, null, InvisibleMinion, obj, false);
            }

            ShowRune();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            // Defensive, idempotent cleanup. On a normal pickup these already ran in OnUpdate (while the
            // units were still alive/visible so the FX_Kill reaches clients); this only covers the relic
            // dying by some other means or game teardown.
            HideRune();
            KillRenderAnchor();
        }

        public void OnUpdate(Buff buff, float diff)
        {
            if (Unit == null || consumed)
            {
                return;
            }

            proximityTimer += diff;
            if (proximityTimer < PROXIMITY_INTERVAL)
            {
                return;
            }
            proximityTimer = 0f;

            // The relic is neutral and heals ANY champion that steps on it. IsValidTarget needs a team flag
            // or it allows nobody — AffectAllSides (friends+enemies+neutral) covers both teams.
            var units = GetUnitsInRange(Unit, Unit.Position, 175f, true,
                SpellDataFlags.AffectHeroes | SpellDataFlags.AffectAllSides);
            if (units.Count >= 1)
            {
                consumed = true;

                // The pickup burst (one-shot), the heal/resource restore, and the +20% move speed buff.
                AddParticle(Unit, null, "TT_Heal_RuneCapture", Unit.Position);
                ApplyRelicPickup(units[0]);
                AddBuff("TT_SpeedShrine_Buff", 5, 1, null, units[0], null);

                // Remove the rune FX + kill the anchor NOW, while both units are still alive and visible, so
                // the FX_Kill broadcast reaches every player the particles were spawned for (the rune is bound
                // to the anchor — don't wait for OnDeactivate, by which point the units are gone).
                HideRune();
                KillRenderAnchor();

                // Consume = kill the relic so the neutral camp respawns a fresh unit (Riot's model — each
                // respawn is a new unit/netid). The death removes this (non-persistent) buff → OnDeactivate
                // runs the same cleanup defensively (idempotent).
                Unit.TakeDamage(Unit, 250000.0f, DamageType.DAMAGE_TYPE_TRUE,
                    DamageSource.DAMAGE_SOURCE_INTERNALRAW, false);
            }
        }

        // Kills the render anchor (TestCubeRender). It is intentionally not Invulnerable (see OnActivate),
        // so the true-damage hit lands and the client drops it + its bound rune FX.
        private void KillRenderAnchor()
        {
            if (InvisibleMinion != null)
            {
                InvisibleMinion.TakeDamage(Unit, 250000.0f, DamageType.DAMAGE_TYPE_TRUE,
                    DamageSource.DAMAGE_SOURCE_INTERNALRAW, false);
                InvisibleMinion = null;
            }
        }

        // Wire-exact (replay): the rune binds to the TestCubeRender anchor (InvisibleMinion) at bone "bottom",
        // caster = the relic, KeywordNetID = 0. affectedByFoW:false so it is sent regardless of fog (paired
        // with the anchor being always-visible → always renders). Lifetime -1 = persistent until removed.
        private void ShowRune()
        {
            buffParticle = AddParticle(Unit, InvisibleMinion, "TT_Heal_Rune", Unit.Position, -1f,
                bone: "bottom", affectedByFoW: false);
            buffParticle2 = AddParticle(Unit, InvisibleMinion, "TT_Heal_RuneWell", Unit.Position, -1f,
                bone: "bottom", affectedByFoW: false);
        }

        private void HideRune()
        {
            if (buffParticle != null)
            {
                buffParticle.SetToRemove();
                buffParticle = null;
            }
            if (buffParticle2 != null)
            {
                buffParticle2.SetToRemove();
                buffParticle2 = null;
            }
        }

        // The Ghost Relic pickup heal + resource restore. The wire shows NO heal buff (only
        // TT_SpeedShrine_Buff), so this is applied directly: a SelfHeal (routed through the engine's heal
        // pipeline so healing reduction / Grievous Wounds apply via OnHeal) plus a PAR-type-aware resource
        // restore (wiki: mana 90→328 by level, energy 25, fury 10).
        private static void ApplyRelicPickup(AttackableUnit unit)
        {
            int level = unit.Stats.Level;

            unit.TakeHeal(unit, 94 + 13 * (level - 1), HealType.SelfHeal);
            AddParticleTarget(unit, unit, "odin_healthpackheal", unit);

            float restore = unit.Stats.ParType switch
            {
                PrimaryAbilityResourceType.Mana => 90 + 14 * (level - 1),
                PrimaryAbilityResourceType.Energy => 25,
                PrimaryAbilityResourceType.Battlefury => 10,
                PrimaryAbilityResourceType.Dragonfury => 10,
                PrimaryAbilityResourceType.Rage => 10,
                PrimaryAbilityResourceType.Gnarfury => 10,
                PrimaryAbilityResourceType.Ferocity => 10,
                _ => 0
            };

            if (restore > 0)
            {
                unit.Stats.CurrentMana = Math.Min(unit.Stats.ManaPoints.Total, unit.Stats.CurrentMana + restore);
                if (unit.Stats.ParType == PrimaryAbilityResourceType.Mana)
                {
                    AddParticleTarget(unit, unit, "summoner_mana", unit);
                }
            }
        }
    }
}
