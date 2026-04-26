using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AkaliSmokeBomb : ISpellScript {
    private const float ShroudDurationSeconds           = 8.0f;
    private const float ShroudDurationMs                = 8000.0f;
    private const float InvisibilityBreakDurationMs     = 650.0f;
    private const float EntryMoveSpeedDurationSeconds   = 1.0f;
    private const float ShroudRadius                    = 450.0f;
    private const float MinBuffDurationSeconds          = 0.1f;
    private const SpellDataFlags SlowTargetFlags =
        SpellDataFlags.AffectHeroes
        | SpellDataFlags.AffectMinions
        | SpellDataFlags.AffectNeutral
        | SpellDataFlags.AffectEnemies
        | SpellDataFlags.AffectFriends
        | SpellDataFlags.NotAffectSelf;

    private readonly HashSet<AttackableUnit> _slowTargets = [];
    private readonly HashSet<Spell> _stealthBreakWatchedSpells = [];

    private ObjAIBase _akali;
    private Spell _spell;
    private Vector2 _shroudPos;
    private Region _bubble;
    private Particle _invisible;
    private bool _shroudActivated;
    private bool _wasInsideShroud;
    private bool _hasAppliedTwilightShroudThisCast;
    private bool _hasSpawnedInvisibilityEntryParticleThisCast;
    private float _shroudElapsedMs;
    private float _invisibilityBreakRemainingMs;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _spell = spell;
        _akali = owner;

        RegisterStealthBreakingSpellListeners();

        ApiEventManager.OnLaunchAttack.AddListener(this, _akali, OnCast);
    }

    private void OnCast(Spell spell) {
        if (!_shroudActivated || spell == _spell) {
            return;
        }

        _invisibilityBreakRemainingMs = InvisibilityBreakDurationMs;
        RemoveBuff(_akali, "AkaliTwilightShroud");
    }

    public void OnSpellPostCast(Spell spell) {
        _spell = spell;
        RegisterStealthBreakingSpellListeners();
        EndShroud();
        _shroudActivated             = true;
        _shroudElapsedMs             = 0f;
        _invisibilityBreakRemainingMs = 0f;
        _wasInsideShroud             = false;
        _hasAppliedTwilightShroudThisCast = false;
        _hasSpawnedInvisibilityEntryParticleThisCast = false;

        var castPosition = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
        _shroudPos = castPosition.LengthSquared() <= float.Epsilon ? _akali.Position : castPosition;

        AddParticlePos(_akali, "akali_smoke_bomb_tar_team_green", _shroudPos, _shroudPos,ShroudDurationSeconds, size: 1f, enemyParticle: "akali_smoke_bomb_tar_team_red");
        
        AddParticle(_akali, null, "akali_smoke_bomb_tar", _shroudPos, ShroudDurationSeconds);
        _bubble = AddPosPerceptionBubble(_shroudPos, ShroudRadius, ShroudDurationSeconds, _akali.Team);

        UpdateAkaliShroudState();
        UpdateSlowTargets();
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) { EndShroud(); }

    public void OnUpdate(float diff) {
        if (!_shroudActivated) {
            return;
        }

        _shroudElapsedMs += diff;
        if (_shroudElapsedMs >= ShroudDurationMs) {
            EndShroud();
            return;
        }

        if (_invisibilityBreakRemainingMs > 0f) {
            _invisibilityBreakRemainingMs = MathF.Max(0f, _invisibilityBreakRemainingMs - diff);
        }

        UpdateAkaliShroudState();
        UpdateSlowTargets();
    }

    private void UpdateAkaliShroudState() {
        var isInsideShroud = Vector2.DistanceSquared(_akali.Position, _shroudPos) <= ShroudRadius * ShroudRadius;
        if (isInsideShroud && !_wasInsideShroud) {
            AddBuff("AkaliTwilightShroudBuff", EntryMoveSpeedDurationSeconds, 1, _spell, _akali, _akali);

            if (!_hasSpawnedInvisibilityEntryParticleThisCast) {
                RemoveParticle(_invisible);
                _invisible = AddParticleTarget(_akali, _akali, "akali_invis_cas.troy", _akali,
                                               lifetime: GetRemainingDurationSeconds());
                _hasSpawnedInvisibilityEntryParticleThisCast = true;
            }
        }
        _wasInsideShroud = isInsideShroud;

        if (isInsideShroud && _invisibilityBreakRemainingMs <= 0f) {
            if (!_akali.HasBuff("AkaliTwilightShroud")) {
                var variables = new BuffVariables();
                variables.Set("isFirstCast", !_hasAppliedTwilightShroudThisCast);
                AddBuff("AkaliTwilightShroud", GetRemainingDurationSeconds(), 1, _spell, _akali, _akali,
                        buffVariables: variables);
                _hasAppliedTwilightShroudThisCast = true;
            }
        } else if (_akali.HasBuff("AkaliTwilightShroud")) {
            RemoveBuff(_akali, "AkaliTwilightShroud");
        }
    }

    private void RegisterStealthBreakingSpellListeners() {
        // OnActivate can run before all champion spells are initialized, so re-scan slots when W is cast.
        for (short i = 0; i < 4; i++) {
            if (!_akali.Spells.TryGetValue(i, out var championSpell) || championSpell == null) {
                continue;
            }

            if (_stealthBreakWatchedSpells.Add(championSpell)) {
                ApiEventManager.OnSpellCast.AddListener(this, championSpell, OnCast);
            }
        }
    }

    private void UpdateSlowTargets() {
        var unitsInShroud = GetUnitsInRange(_akali, _shroudPos, ShroudRadius, true, SlowTargetFlags).ToHashSet();

        foreach (var unit in _slowTargets.Where(unit => !unitsInShroud.Contains(unit)).ToList()) {
            RemoveBuff(unit, "AkaliTwilightShroudDebuff");
            _slowTargets.Remove(unit);
        }

        foreach (var unit in unitsInShroud.Where(unit => !_slowTargets.Contains(unit))) {
            var variables = new BuffVariables();
            variables.Set("slowAmount", 0.14f + 0.04f * (_spell.CastInfo.SpellLevel - 1));
            variables.Set("attackSpeedSlowAmount", 0f);
            AddBuff("AkaliTwilightShroudDebuff", GetRemainingDurationSeconds(), 1, _spell, unit, _akali,
                    buffVariables: variables);
            _slowTargets.Add(unit);
        }
    }

    private void EndShroud() {
        if (_akali != null && _akali.HasBuff("AkaliTwilightShroud")) {
            RemoveBuff(_akali, "AkaliTwilightShroud");
        }

        foreach (var unit in _slowTargets.ToList()) {
            RemoveBuff(unit, "AkaliTwilightShroudDebuff");
        }

        _slowTargets.Clear();
        RemoveParticle(_invisible);
        _invisible = null;
        _shroudActivated = false;
        _wasInsideShroud = false;
        _hasAppliedTwilightShroudThisCast = false;
        _hasSpawnedInvisibilityEntryParticleThisCast = false;
        _invisibilityBreakRemainingMs = 0f;
    }

    private float GetRemainingDurationSeconds() {
        return MathF.Max(MinBuffDurationSeconds, (ShroudDurationMs - _shroudElapsedMs) / 1000.0f);
    }
}
