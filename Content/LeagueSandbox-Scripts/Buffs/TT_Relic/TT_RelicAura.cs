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
            PersistsThroughDeath = true,
        };

        public StatsModifier StatsModifier { get; private set; }

        bool setToKill;
        Buff thisBuff;
        Particle buffParticle;
        Particle buffParticle2;
        AttackableUnit Unit;
        float timer = 250f;
        Minion InvisibleMinion;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            thisBuff = buff;
            Unit = unit;

            buff.SetStatusEffect(StatusFlags.Targetable, false);
            buff.SetStatusEffect(StatusFlags.Invulnerable, true);
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
            SetStatus(InvisibleMinion, StatusFlags.Invulnerable, true);

            if (unit is ObjAIBase obj)
            {
                AddBuff("ResistantSkinDragon", 25000f, 1, null, InvisibleMinion, obj, false);
            }

            // Wire-exact (replay): the rune binds to the TestCubeRender anchor (InvisibleMinion) at bone
            // "bottom", caster = the relic, KeywordNetID = 0. affectedByFoW:false so it is sent regardless of
            // fog (paired with the anchor being always-visible above → always renders).
            buffParticle = AddParticle(unit, InvisibleMinion, "TT_Heal_Rune", unit.Position, -1f,
                bone: "bottom", affectedByFoW: false);
            buffParticle2 = AddParticle(unit, InvisibleMinion, "TT_Heal_RuneWell", unit.Position, -1f,
                bone: "bottom", affectedByFoW: false);

            setToKill = false;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            buffParticle.SetToRemove();
            buffParticle2.SetToRemove();

            SetStatus(unit, StatusFlags.Targetable, true);
            SetStatus(unit, StatusFlags.Invulnerable, false);

            unit.TakeDamage(unit, 250000.0f, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_INTERNALRAW,
                false);
            InvisibleMinion.TakeDamage(unit, 250000.0f, DamageType.DAMAGE_TYPE_TRUE,
                DamageSource.DAMAGE_SOURCE_INTERNALRAW, false);

            SetStatus(unit, StatusFlags.NoRender, true);
        }

        public void OnUpdate(float diff)
        {
            if (setToKill)
            {
                thisBuff.DeactivateBuff();
            }

            timer += diff;
            if (Unit != null && timer >= 250)
            {
                // The relic is neutral and heals ANY champion that steps on it. IsValidTarget needs a team
                // flag or it allows nobody — AffectAllSides (friends+enemies+neutral) covers both teams.
                var units = GetUnitsInRange(Unit, Unit.Position, 175f, true,
                    SpellDataFlags.AffectHeroes | SpellDataFlags.AffectAllSides);
                if (units.Count >= 1)
                {
                    if (!setToKill)
                    {
                        AddParticle(Unit, null, "TT_Heal_RuneCapture", Unit.Position);
                        ApplyRelicPickup(units[0]);
                        // The +20% move speed (decays over 5s); the heal/resource restore above is a direct
                        // self-heal (no networked buff) — matching the wire, which only sends this buff.
                        AddBuff("TT_SpeedShrine_Buff", 5, 1, null, units[0], null);

                        setToKill = true;
                    }
                }

                timer = 0;
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
                PrimaryAbilityResourceType.MANA => 90 + 14 * (level - 1),
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
                if (unit.Stats.ParType == PrimaryAbilityResourceType.MANA)
                {
                    AddParticleTarget(unit, unit, "summoner_mana", unit);
                }
            }
        }
    }
}