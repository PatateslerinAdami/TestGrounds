using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class Radiance : IBuffGameScript {
    private const float AuraRange = 1100f;

    // Pulse aura (replay-verified vs Taric RadianceAura, replays/7e3c520a…rlp.json 2026-07-21):
    // re-apply a short fixed-duration RENEW buff every ~1000ms to all allied champions in range;
    // allies that leave range let it expire naturally (~1 tick later) — no explicit remove, no
    // membership tracking. The 1.25s duration = tick + margin so the buff never lapses between ticks.
    private const float AuraRefreshIntervalMs = 1000f;
    private const float RadianceAuraDurationSeconds = 1.25f;

    private ObjAIBase _taric;
    private Buff _buff;
    private Particle _auraParticle;
    private Particle _auraParticle1;
    private Particle _shoulderParticle;
    private float _auraRefreshTimer = AuraRefreshIntervalMs;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _taric = buff.SourceUnit;
        _buff = buff;

        var selfBonus = 30f + 20f * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.AttackDamage.FlatBonus = selfBonus;
        StatsModifier.AbilityPower.FlatBonus = selfBonus;
        unit.AddStatModifier(StatsModifier);

        _auraParticle = AddParticleTarget(_taric, unit, "Taric_GemStorm_Aura", unit, buff.Duration, size: 1.25f);
        _auraParticle1 = AddParticleTarget(_taric, unit, "taricgemstorm", unit, buff.Duration, size: 1.25f);
        RefreshAura();
    }

    public void OnUpdate(Buff buff, float diff) {
        if (_taric.IsDead) {
            _buff.DeactivateBuff();
            return;
        }

        _auraRefreshTimer += diff;
        if (_auraRefreshTimer < AuraRefreshIntervalMs) return;

        // Subtract the interval (no drift) rather than resetting to 0.
        _auraRefreshTimer -= AuraRefreshIntervalMs;
        RefreshAura();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        // Ally auras are short pulse buffs — they expire on their own ~1.25s after the last tick,
        // matching Riot's natural-expiry Remove; no explicit teardown needed for the ally buffs.
        RemoveParticle(_auraParticle);
        RemoveParticle(_auraParticle1);
        RemoveParticle(_shoulderParticle);
    }

    private void RefreshAura() {
        // Re-apply to every allied champion currently in range each tick. RENEW_EXISTING means an
        // existing instance is refreshed (BuffUpdateCount rt=0) rather than re-added; newcomers get a
        // fresh add. Fixed 1.25s duration — NOT the remaining ult duration.
        foreach (var ally in GetChampionsInRange(_taric.Position, AuraRange, true)
                     .Where(ally => ally.Team == _taric.Team && ally != _taric && !ally.IsDead)) {
            AddBuff("RadianceAura", RadianceAuraDurationSeconds, 1, _buff.OriginSpell, ally, _taric);
        }
    }
}
