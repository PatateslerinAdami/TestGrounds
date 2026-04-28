using System;
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

public class trundledesecrate : ISpellScript {
    private const float ZoneDurationSeconds = 8.0f;
    private const float ZoneRadius          = 775.0f;
    private const float ZoneDurationMs      = ZoneDurationSeconds * 1000.0f;

    private ObjAIBase _trundle;
    private Spell     _spell;
    private Vector2   _end;
    private PeriodicTicker _zoneTicker;
    private bool      _zoneActive;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _trundle = owner;
        _spell = spell;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _end = end;
        _zoneTicker.Reset();
        _zoneActive = true;
        PlayAnimation(_trundle,"Spell2");
    }

    public void OnSpellCast(Spell spell) {
        AddParticleTarget(_trundle, _trundle, "Trundle_W_Jump", _trundle);
    }

    public void OnUpdate(float diff) {
        if (!_zoneActive) return;

        var expiredTicks = _zoneTicker.ConsumeTicks(diff, ZoneDurationMs, false, 1, 1);
        if (expiredTicks > 0) {
            _zoneActive = false;
            if (_trundle.HasBuff("TrundleDesecrateBuffs")) RemoveBuff(_trundle, "TrundleDesecrateBuffs");
            return;
        }

        if (Vector2.Distance(_trundle.Position, _end) > ZoneRadius) {
            if (!_trundle.HasBuff("TrundleDesecrateBuffs")) return;
            RemoveBuff(_trundle, "TrundleDesecrateBuffs");
            return;
        }

        if (_trundle.HasBuff("TrundleDesecrateBuffs")) return;
        var remainingDuration = _zoneTicker.GetRemainingMsUntilNextTick(ZoneDurationMs, false, 1) * 0.001f;
        if (remainingDuration > 0.0f)
            AddBuff("TrundleDesecrateBuffs", remainingDuration, 1, _spell, _trundle, _trundle);
    }

    public void OnSpellPostCast(Spell spell) {
        AddParticlePos(_trundle, "Trundle_W_ground", _end, _end,8f);
        AddParticlePos(_trundle, "Trundle_W_Green_Ring", _end, _end,8f, enemyParticle: "Trundle_W_Red_Ring");
    }
}
