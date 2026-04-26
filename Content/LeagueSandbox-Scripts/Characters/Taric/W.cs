using System.Collections.Generic;
using System.Linq;
using CharScripts;
using GameServerCore;
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

namespace Spells;

public class Shatter : ISpellScript {
    private const float AuraRange = 1100f;
    private const float AuraRefreshIntervalMs = 250f;
    private const float GlobalCleanupRange = 25000f;
    private const float AuraArmorPercent = 0.12f;
    private const float AuraFeedbackMultiplier = AuraArmorPercent / (1.0f - AuraArmorPercent);

    private readonly Dictionary<AttackableUnit, StatsModifier> _auraModifiers = new();
    private ObjAIBase _taric;
    private Spell _spell;
    private float _auraRefreshTimer = AuraRefreshIntervalMs;
    private float _currentAuraArmorBonus;
    private int _selfBonusSpellLevel;
    private bool _passiveEnabled;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _taric = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _taric, OnUpdateStats);
    }

    public void OnSpellCast(Spell spell) {
        DisablePassive();
    }

    public void OnSpellPostCast(Spell spell) {
        var preCastArmor = _taric.Stats.Armor.Total;
        var damage = 40f + 40f * (_spell.CastInfo.SpellLevel - 1) + preCastArmor * 0.2f;
        var armorReduction = 5f + 5f * (_spell.CastInfo.SpellLevel - 1) + preCastArmor * 0.05f;
        AddParticleTarget(_taric, _taric, "Shatter_nova", _taric);
        var enemies = GetUnitsInRange(_taric, _taric.Position, 375f, true,
                                      SpellDataFlags.AffectEnemies| SpellDataFlags.AffectHeroes |
                                      SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var enemy in  enemies) {
            var variables = new BuffVariables();
            variables.Set("armorReduction", armorReduction);
            AddBuff("Shatter", 4f, 1, spell, enemy, _taric, buffVariables: variables);
            enemy.TakeDamage(_taric, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                             DamageResultType.RESULT_NORMAL);
        }
    }

    public void OnUpdate(float diff) {
        if (!ShouldEnablePassive()) {
            if (_passiveEnabled) DisablePassive();
            return;
        }

        EnsureSelfBonus();

        _auraRefreshTimer += diff;
        if (_auraRefreshTimer < AuraRefreshIntervalMs) return;

        _auraRefreshTimer = 0f;
        RefreshAura();
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        SetSpellToolTipVar(_taric, 0, _taric.Stats.Armor.Total * 0.12f, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_taric, 1, _taric.Stats.Armor.Total * 0.2f, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_taric, 2, _taric.Stats.Armor.Total * 0.05f, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }

    private bool ShouldEnablePassive() {
        return _spell.CastInfo.SpellLevel > 0 && _spell.CurrentCooldown <= 0 && !_taric.IsDead;
    }

    private void EnsureSelfBonus() {
        if (!_passiveEnabled) {
            _passiveEnabled = true;
            _auraRefreshTimer = AuraRefreshIntervalMs;
        }

        if (_selfBonusSpellLevel == _spell.CastInfo.SpellLevel && HasBuff(_taric, "ShatterSelfBonus")) return;

        AddBuff("ShatterSelfBonus", 25000f, 1, _spell, _taric, _taric, infiniteduration: true);
        _selfBonusSpellLevel = _spell.CastInfo.SpellLevel;
    }

    private void DisablePassive() {
        _passiveEnabled = false;
        _selfBonusSpellLevel = 0;
        _auraRefreshTimer = AuraRefreshIntervalMs;
        _currentAuraArmorBonus = 0f;

        RemoveBuff(_taric, "ShatterSelfBonus");
        foreach (var target in _auraModifiers.Keys.ToList()) {
            RemoveAuraFromTarget(target);
        }

        foreach (var ally in GetChampionsInRange(_taric.Position, GlobalCleanupRange, true)
                     .Where(ally => ally.Team == _taric.Team)) {
            RemoveBuff(ally, "ShatterAura");
        }
    }

    private void RefreshAura() {
        if (!_auraModifiers.ContainsKey(_taric)) _currentAuraArmorBonus = 0f;

        var armorWithoutAura = _taric.Stats.Armor.Total - _currentAuraArmorBonus;
        var armorBonus = armorWithoutAura * AuraFeedbackMultiplier;
        _currentAuraArmorBonus = armorBonus;

        var currentTargets = GetChampionsInRange(_taric.Position, AuraRange, true)
                             .Where(ally => ally.Team == _taric.Team && !ally.IsDead)
                             .Cast<AttackableUnit>()
                             .ToHashSet();
        currentTargets.Add(_taric);

        foreach (var target in _auraModifiers.Keys.Where(target => !currentTargets.Contains(target)).ToList()) {
            RemoveAuraFromTarget(target);
        }

        foreach (var target in currentTargets) {
            UpdateAuraTarget(target, armorBonus);
        }
    }

    private void UpdateAuraTarget(AttackableUnit target, float armorBonus) {
        if (!HasBuff(target, "ShatterAura")) {
            AddBuff("ShatterAura", 25000f, 1, _spell, target, _taric, infiniteduration: true);
        }

        if (!_auraModifiers.TryGetValue(target, out var modifier)) {
            modifier = new StatsModifier();
            _auraModifiers[target] = modifier;
        } else {
            target.RemoveStatModifier(modifier);
        }

        modifier.Armor.FlatBonus = armorBonus;
        target.AddStatModifier(modifier);
    }

    private void RemoveAuraFromTarget(AttackableUnit target) {
        if (_auraModifiers.Remove(target, out var modifier)) {
            target.RemoveStatModifier(modifier);
        }

        RemoveBuff(target, "ShatterAura");
    }
}
