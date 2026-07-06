using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TormentedSoil : ISpellScript {
    private const float TickIntervalMs = 1000f;
    private const int   MaxTickCount   = 5;
    private const float ZoneRadius     = 280f;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        // 4.20 TormentedSoil.lua: the pool ticks pass through spell shields without consuming
        // them (SpellMetaData.txt's "won't break shields" case). Matters once the W damage is
        // routed through Spell.ApplyEffects (its spell-shield gate reads this flag).
        DoesntBreakShields = true,
    };

    public void OnSpellPostCast(Spell spell) {
        var owner = spell.CastInfo.Owner;
        var pos   = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
        var level = spell.CastInfo.SpellLevel;

        AddParticlePos(owner, "Morgana_Base_W_Tar_green", pos, pos, 5f, enemyParticle: "Morgana_Base_W_Tar_red");

        // Riot 4.20 (replay-verified e8b501e2): no spawned minion/zone object — the zone is just a
        // position-anchored 5s FX plus server-side periodic damage at a fixed point. Each cast drives
        // its OWN self-terminating tick chain (no shared list / no per-slot script state): the first
        // tick lands immediately, then one every TickIntervalMs up to MaxTickCount. The timers run on
        // the caster's game loop and keep firing through her death (Tormented Soil PersistsThroughDeath).
        ApplyTick(owner, pos, level);
        ScheduleTick(owner, pos, level, 1);
    }

    private void ScheduleTick(ObjAIBase owner, Vector2 pos, int level, int tickIndex) {
        if (tickIndex >= MaxTickCount) {
            return;
        }

        owner.RegisterTimer(new GameScriptTimer(TickIntervalMs / 1000f, () => {
            ApplyTick(owner, pos, level);
            ScheduleTick(owner, pos, level, tickIndex + 1);
        }));
    }

    private void ApplyTick(ObjAIBase owner, Vector2 pos, int level) {
        var unitsInRange = GetUnitsInRange(owner, pos, ZoneRadius, true,
                                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                           SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var unit in unitsInRange) {
            var missingHealthPct = 0f;
            if (unit.Stats.HealthPoints.Total > 0f) {
                missingHealthPct = (unit.Stats.HealthPoints.Total - unit.Stats.CurrentHealth) / unit.Stats.HealthPoints.Total;
            }

            missingHealthPct = missingHealthPct switch {
                < 0f => 0f,
                > 1f => 1f,
                _    => missingHealthPct
            };

            var baseMinDamage = 24 + 12f * (level - 1);
            var baseMaxDamage = 36 + 21f * (level - 1);
            var baseDamage    = baseMinDamage + (baseMaxDamage - baseMinDamage) * missingHealthPct;

            var apMinDamage = owner.Stats.AbilityPower.Total * 0.2f;
            var apMaxDamage = owner.Stats.AbilityPower.Total * 0.35f;
            var apDamage    = apMinDamage + (apMaxDamage - apMinDamage) * missingHealthPct;

            AddParticle(owner, unit, "FireFeet_buf", unit.Position, bone: "L_foot");
            AddParticle(owner, unit, "FireFeet_buf", unit.Position, bone: "R_foot");
            unit.TakeDamage(owner, (baseDamage + apDamage) * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                            DamageResultType.RESULT_NORMAL);
        }
    }
}
