using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TormentedSoil : ISpellScript {
    private class ActiveZone {
        public Vector2 Position;
        public float   TimerMs;
        public short   TickCount;
        public int     SpellLevel;
    }

    private readonly List<ActiveZone> _activeZones = new();
    private          ObjAIBase        _morgana;
    private const float     MaxTickTime  = 500f;
    private const short     MaxTickCount = 10;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _morgana = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
    }

    public void OnSpellCast(Spell spell) {
        
    }

    public void OnSpellPostCast(Spell spell) {
        var end = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);

        _activeZones.Add(new ActiveZone {
            Position   = end,
            TimerMs    = MaxTickTime,
            TickCount  = 0,
            SpellLevel = spell.CastInfo.SpellLevel
        });

        AddParticlePos(_morgana, "Morgana_Base_W_Tar_green", end, end, 5f, enemyParticle: "Morgana_Base_W_Tar_red");
    }

    public void OnUpdate(float diff) {
        for (var i = _activeZones.Count - 1; i >= 0; i--) {
            var zone = _activeZones[i];
            zone.TimerMs += diff;

            if (zone.TimerMs <= MaxTickTime || zone.TickCount >= MaxTickCount) {
                if (zone.TickCount >= MaxTickCount) {
                    _activeZones.RemoveAt(i);
                }

                continue;
            }

            var unitsInRange = GetUnitsInRange(_morgana, zone.Position, 280f, true,
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

                var baseMinDamage = 24 + 12f * (zone.SpellLevel - 1);
                var baseMaxDamage = 36 + 21f * (zone.SpellLevel - 1);
                var baseDamage    = baseMinDamage + (baseMaxDamage - baseMinDamage) * missingHealthPct;

                var apMinDamage = _morgana.Stats.AbilityPower.Total * 0.2f;
                var apMaxDamage = _morgana.Stats.AbilityPower.Total * 0.35f;
                var apDamage    = apMinDamage + (apMaxDamage - apMinDamage) * missingHealthPct;
                
                AddParticle(_morgana, unit,"FireFeet_buf", unit.Position, bone: "L_foot");
                AddParticle(_morgana, unit,"FireFeet_buf", unit.Position, bone: "R_foot");
                unit.TakeDamage(_morgana, (baseDamage + apDamage)*0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                                DamageResultType.RESULT_NORMAL);
            }

            zone.TickCount++;
            zone.TimerMs = 0f;

            if (zone.TickCount >= MaxTickCount) {
                _activeZones.RemoveAt(i);
            }
        }
    }
}
