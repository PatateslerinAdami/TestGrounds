using System.Collections.Generic;
using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptVayne : ICharScript {
    private const float NightHunterRange = 2000.0f;
    private const float NightHunterPersistDuration = 2.0f;
    private readonly Dictionary<uint, float> _recentSightDurations = new();
    private ObjAIBase                        _vayne;
    private Spell                            _spell;

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _vayne = owner;
        _spell = spell;
        _recentSightDurations.Clear();
    }

    public void OnUpdate(float diff) {
        UpdateRecentSightDurations(diff);

        var shouldHaveBuff = GetUnitsInRange(_vayne, _vayne.Position, NightHunterRange, true,
                                             SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)
            .Any(IsNightHunterTarget);
        var hasBuff = _vayne.HasBuff("VayneNightHunter");

        switch (shouldHaveBuff)
        {
            case true when !hasBuff:
                AddBuff("VayneNightHunter", 25000f, 1, _spell, _vayne, _vayne);
                break;
            case false when hasBuff:
                RemoveBuff(_vayne, "VayneNightHunter");
                break;
        }
    }

    private bool IsNightHunterTarget(AttackableUnit unit) {
        if (unit.IsDead || !IsInFront(_vayne, unit)) {
            return false;
        }

        if (!unit.IsVisibleByTeam(_vayne.Team))
            return _recentSightDurations.TryGetValue(unit.NetId, out var remainingDuration) &&
                   remainingDuration > 0.0f;
        _recentSightDurations[unit.NetId] = NightHunterPersistDuration;
        return true;

    }

    private void UpdateRecentSightDurations(float diff) {
        if (_recentSightDurations.Count == 0) {
            return;
        }

        var expiredTargets = new List<uint>();
        foreach (var targetId in _recentSightDurations.Keys.ToList()) {
            var remainingDuration = _recentSightDurations[targetId] - diff / 1000.0f;
            if (remainingDuration > 0.0f) {
                _recentSightDurations[targetId] = remainingDuration;
            } else {
                expiredTargets.Add(targetId);
            }
        }

        foreach (var targetId in expiredTargets) {
            _recentSightDurations.Remove(targetId);
        }
    }
}
