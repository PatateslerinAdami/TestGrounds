using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class EvelynnStealthMarker : IBuffGameScript {
    private const float RevealRange        = 600f;
    private const float WarningRange       = 800f;
    private const float StealthFadeOpacity = 0.4f;
    private const float InitialStealthFadeSeconds = 0.5f;
    private const float RevealStateFadeSeconds = 0.05f;
    private const float PassiveTickMs      = 1000f;
    private const float ExclamationRefreshMs = 1000f;
    private const float PassiveManaMissing = 0.02f;

    private Fade      _id;
    private ObjAIBase _evelynn;
    private Spell     _spell;
    private Particle  _stealthRing, _exclamationMark;
    private float _passiveTickTimer;
    private float _exclamationRefreshTimer;
    private bool _isRevealed;
    private bool _isExclamationVisible;
    private readonly HashSet<AttackableUnit> _redEyeTargets    = [];
    private readonly HashSet<AttackableUnit> _yellowEyeTargets = [];

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INVISIBILITY,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _evelynn = ownerSpell.CastInfo.Owner;
        _spell   = ownerSpell;
        _isRevealed      = false;
        _isExclamationVisible = false;
        _passiveTickTimer = 0f;
        _exclamationRefreshTimer = 0f;
        _redEyeTargets.Clear();
        _yellowEyeTargets.Clear();

        SetStealthState(true);
        SetStatus(_evelynn, StatusFlags.Ghosted,   true);
        AddParticleTarget(_evelynn, null, "EvelynnPoof",          _evelynn); //puff of purple smoke particle
        _evelynn.SetAnimStates(new Dictionary<string, string> {
            { "Idle1", "Idle2" },
            { "run", "Run2" }
        });
        _stealthRing = AddParticleTarget(_evelynn, _evelynn, "Evelynn_Ring_Green", _evelynn, size: 0.8f, lifetime: 25000000f, unitOnly: _evelynn); //grey circle indicator size has to be 0.8f so it scales correctly visually
        SetEnemyVisibility(false);
        ApplyStealthFade(InitialStealthFadeSeconds);
    }

    public void OnUpdate(float diff) {
        if (_evelynn == null || _evelynn.IsDead) {
            return;
        }

        UpdatePassiveManaRegen(diff);

        var nearbyEnemies = GetUnitsInRange(_evelynn, _evelynn.Position, WarningRange, true,
                                            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes);
        var nextRedEyes    = new HashSet<AttackableUnit>();
        var nextYellowEyes = new HashSet<AttackableUnit>();

        var shouldReveal = false;
        var shouldShowExclamation = false;

        foreach (var unit in nearbyEnemies) {
            if (unit is not Champion enemyChampion || enemyChampion.IsDead || enemyChampion.Team == _evelynn.Team) {
                continue;
            }

            var distanceSquared   = Vector2.DistanceSquared(_evelynn.Position, enemyChampion.Position);
            var evelynnCanSeeEnemy = TeamHasVision(_evelynn.Team, enemyChampion);

            switch (distanceSquared) {
                case <= RevealRange * RevealRange: {
                    shouldShowExclamation =  true;
                    shouldReveal          =  true;
                    if (evelynnCanSeeEnemy) {
                        nextRedEyes.Add(enemyChampion);
                    }

                    break;
                }
                case <= WarningRange * WarningRange when evelynnCanSeeEnemy:
                    nextYellowEyes.Add(enemyChampion);
                    break;
            }
        }

        UpdateEyeBuffs(nextRedEyes, nextYellowEyes);
        UpdateRevealState(shouldReveal);
        UpdateExclamationState(shouldShowExclamation, diff);
    }

    private void UpdatePassiveManaRegen(float diff) {
        _passiveTickTimer += diff;
        while (_passiveTickTimer >= PassiveTickMs) {
            _passiveTickTimer -= PassiveTickMs;
            var maxMana = _evelynn.Stats.ManaPoints.Total;
            if (maxMana <= 0f) {
                continue;
            }

            var missingMana = maxMana - _evelynn.Stats.CurrentMana;
            if (missingMana <= 0f) {
                continue;
            }

            _evelynn.Stats.CurrentMana = System.Math.Min(maxMana, _evelynn.Stats.CurrentMana + missingMana * PassiveManaMissing);
        }
    }

    private void UpdateRevealState(bool shouldReveal) {
        if (_isRevealed == shouldReveal) {
            return;
        }

        _isRevealed = shouldReveal;
        SetStealthState(!shouldReveal);
        SetEnemyVisibility(shouldReveal);
        _evelynn.SetAnimStates(new Dictionary<string, string> {
            { "Idle1", shouldReveal ? "Idle3" : "Idle2" },
            { "run", shouldReveal ? "Run3" : "Run2" }
        });
        ApplyStealthFade(RevealStateFadeSeconds);
    }

    private void SetStealthState(bool stealthed) {
        SetStatus(_evelynn, StatusFlags.Stealthed, stealthed);
    }

    private void ApplyStealthFade(float fadeTimeSeconds) {
        _id = PushCharacterFade(_evelynn, StealthFadeOpacity, fadeTimeSeconds, _id);
    }

    private void UpdateExclamationState(bool shouldShowExclamation, float diff) {
        if (!shouldShowExclamation) {
            if (_isExclamationVisible) {
                RemoveParticle(_exclamationMark);
                _exclamationMark      = null;
                _isExclamationVisible = false;
            }

            _exclamationRefreshTimer = 0f;
            return;
        }

        _exclamationRefreshTimer += diff;

        var shouldReapply = !_isExclamationVisible
                            || _exclamationMark == null
                            || _exclamationRefreshTimer >= ExclamationRefreshMs;
        if (!shouldReapply) {
            return;
        }

        RemoveParticle(_exclamationMark);
        _exclamationMark = AddParticleTarget(
            _evelynn,
            _evelynn,
            "Evelynn-Yikes",
            _evelynn,
            size: 0.8f,
            lifetime: 25000000f,
            bone: "C_BUFFBONE_GLB_HEAD_LOC",
            teamOnly: _evelynn.Team
        ); //exclamtion mark when spotted size has to be 0.8f so it scales correctly visually

        _isExclamationVisible = true;
        _exclamationRefreshTimer = 0f;
    }

    private void UpdateEyeBuffs(HashSet<AttackableUnit> nextRedEyes, HashSet<AttackableUnit> nextYellowEyes) {
        var removeRed = new List<AttackableUnit>();
        foreach (var unit in _redEyeTargets) {
            if (!nextRedEyes.Contains(unit) || unit == null || unit.IsDead) {
                removeRed.Add(unit);
            }
        }

        foreach (var unit in removeRed) {
            if (unit != null) {
                RemoveBuff(unit, "EvelynnRedEye");
            }
            _redEyeTargets.Remove(unit);
        }

        var removeYellow = new List<AttackableUnit>();
        foreach (var unit in _yellowEyeTargets) {
            if (!nextYellowEyes.Contains(unit) || unit == null || unit.IsDead || nextRedEyes.Contains(unit)) {
                removeYellow.Add(unit);
            }
        }

        foreach (var unit in removeYellow) {
            if (unit != null) {
                RemoveBuff(unit, "EvelynnYellowEye");
            }
            _yellowEyeTargets.Remove(unit);
        }

        foreach (var unit in nextRedEyes) {
            if (unit == null || unit.IsDead) {
                continue;
            }

            if (_yellowEyeTargets.Contains(unit)) {
                RemoveBuff(unit, "EvelynnYellowEye");
                _yellowEyeTargets.Remove(unit);
            }

            if (_redEyeTargets.Contains(unit)) {
                continue;
            }

            AddBuff("EvelynnRedEye", 250000f, 1, _spell, unit, _evelynn, true);
            _redEyeTargets.Add(unit);
        }

        foreach (var unit in nextYellowEyes) {
            if (unit == null || unit.IsDead || _redEyeTargets.Contains(unit) || _yellowEyeTargets.Contains(unit)) {
                continue;
            }

            AddBuff("EvelynnYellowEye", 250000f, 1, _spell, unit, _evelynn, true);
            _yellowEyeTargets.Add(unit);
        }
    }

    private void SetEnemyVisibility(bool visible) {
        switch (_evelynn.Team) {
            case TeamId.TEAM_BLUE:
                _evelynn.SetVisibleByTeam(TeamId.TEAM_PURPLE, visible);
                break;
            case TeamId.TEAM_PURPLE:
                _evelynn.SetVisibleByTeam(TeamId.TEAM_BLUE, visible);
                break;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_stealthRing);
        RemoveParticle(_exclamationMark);
        _exclamationMark          = null;
        _isExclamationVisible     = false;
        _exclamationRefreshTimer  = 0f;

        foreach (var target in _redEyeTargets) {
            if (target != null) {
                RemoveBuff(target, "EvelynnRedEye");
            }
        }
        _redEyeTargets.Clear();

        foreach (var target in _yellowEyeTargets) {
            if (target != null) {
                RemoveBuff(target, "EvelynnYellowEye");
            }
        }
        _yellowEyeTargets.Clear();

        _evelynn.SetAnimStates(new Dictionary<string, string> {
            { "idle1", "" },
            { "run", "" },
        });
        SetStealthState(false);
        _evelynn.SetStatus(StatusFlags.RevealSpecificUnit, false);
        if (!_evelynn.HasBuff("EvelynnW") && !_evelynn.HasBuff("EvelynnE")) {
            _evelynn.SetStatus(StatusFlags.Ghosted, false);
        }
        SetEnemyVisibility(true);
    }
}
