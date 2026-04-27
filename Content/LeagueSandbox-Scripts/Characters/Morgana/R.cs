using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SoulShackles : ISpellScript {
    private          ObjAIBase      _morgana;
    private          AttackableUnit _target;
    private          Spell          _spell;
    private          Particle       _indicatorRing;
    private          float          _timerMs     = 0f;
    private const    float          TetherDurationMs = 3000f;
    private const    float          TetherRange  = 630f;
    private readonly List<Particle> _particles   = [];

    private readonly Dictionary<AttackableUnit, List<Particle>> _enemiesTethered =
        new Dictionary<AttackableUnit, List<Particle>>();

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _morgana = owner; }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        _spell  = spell;
    }

    public void OnSpellPostCast(Spell spell) {
        _enemiesTethered.Clear();
        var unitsInRange = GetUnitsInRange(_morgana, _morgana.Position, TetherRange, true,
                                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes);
        foreach (var unit in unitsInRange) {
            _enemiesTethered.Add(
                unit,
                [
                    AddParticleTarget(_morgana, _morgana, "Morgana_Base_R_Beam", unit, 3f, bone: "spine",
                                      targetBone: "spine"),
                    AddParticleTarget(_morgana, unit, "Morgana_Base_R_Tar", unit, 3f)
                ]);
            var ap  = _morgana.Stats.AbilityPower.Total * 0.8f;
            var dmg = 150f + 75f * (_spell.CastInfo.SpellLevel - 1) + ap;
            unit.TakeDamage(_morgana, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                            DamageResultType.RESULT_NORMAL);
            AddBuff("SoulShackles", 3f, 1, spell, unit, _morgana);
        }

        _timerMs       = 0f;
        _indicatorRing = AddParticleTarget(_morgana, _morgana, "Morgana_base_R_Indicator_Ring", _morgana, 3f);
    }

    public void OnUpdate(float diff) {
        if (_enemiesTethered.Count == 0) return;

        _timerMs += diff;
        var unitsInRange = GetUnitsInRange(_morgana, _morgana.Position, TetherRange, true,
                                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes);
        var brokenTethers = _enemiesTethered.Keys
            .Where(enemy => !unitsInRange.Contains(enemy) || enemy.IsDead)
            .ToList();

        foreach (var enemy in brokenTethers) {
            _enemiesTethered.TryGetValue(enemy, out var tetherParticles);
            if (tetherParticles != null) {
                foreach (var tetherParticle in tetherParticles) { RemoveParticle(tetherParticle); }
            }

            RemoveBuff(enemy, "SoulShackles");
            _enemiesTethered.Remove(enemy);
        }

        if (_timerMs < TetherDurationMs) {
            if (_enemiesTethered.Count == 0) RemoveParticle(_indicatorRing);
            return;
        }

        foreach (var enemy in _enemiesTethered.Keys.ToList()) {
            if (!_enemiesTethered.TryGetValue(enemy, out var tetherParticles)) continue;

            foreach (var tetherParticle in tetherParticles) { RemoveParticle(tetherParticle); }

            var ap  = _morgana.Stats.AbilityPower.Total * 0.8f;
            var dmg = 150f + 75f * (_spell.CastInfo.SpellLevel - 1) + ap;
            AddParticleTarget(_morgana, enemy, "Morgana_Base_R_Tar_Explode", enemy);
            AddBuff("Stun", 1.5f, 1, _spell, enemy, _morgana);
            enemy.TakeDamage(_morgana, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                             DamageResultType.RESULT_NORMAL);
            RemoveBuff(enemy, "SoulShackles");
            _enemiesTethered.Remove(enemy);
        }

        RemoveParticle(_indicatorRing);
    }
}
