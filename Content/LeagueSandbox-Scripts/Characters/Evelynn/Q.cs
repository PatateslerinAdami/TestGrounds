using System.Collections.Generic;
using System.Linq;
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

public class EvelynnQ : ISpellScript {
    private const float HateSpikeRange = 500f;
    private const SpellDataFlags HateSpikeFlags = SpellDataFlags.AffectEnemies
                                                  | SpellDataFlags.AffectHeroes
                                                  | SpellDataFlags.AffectNeutral
                                                  | SpellDataFlags.AffectMinions;

    private ObjAIBase _evelynn;
    private Spell _spell;
    private AttackableUnit _lastHateSpikeTarget;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _evelynn = owner;
        _spell   = spell;
        ApiEventManager.OnLevelUpSpell.AddListener(this, _spell, OnQLevelUpSpell);
        ApiEventManager.OnUpdateStats.AddListener(this, _evelynn, OnUpdateStats);
        ApiEventManager.OnDealDamage.AddListener(this, _evelynn, OnDealDamage);
        ApiEventManager.OnLaunchAttack.AddListener(this, _evelynn, OnLaunchAttack);
        UpdateUltSeal();
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var castTarget = SelectHateSpikeTarget();
        if (castTarget == null) {
            return;
        }

        SpellCast(_evelynn, 3, SpellSlotType.ExtraSlots, true, castTarget, _evelynn.Position);
    }

    public void OnUpdate(float diff) {
        UpdateUltSeal();
    }

    private void OnQLevelUpSpell(Spell spell) {
        UpdateUltSeal();
    }

    private void UpdateUltSeal() {
        var shouldSeal = ShouldSealUlt();
        SealSpellSlot(
            _evelynn,
            SpellSlotType.SpellSlots, 0,
            SpellbookType.SPELLBOOK_CHAMPION,
            shouldSeal
        );
    }

    private bool ShouldSealUlt() {
        var isLearned       = _spell != null && _spell.CastInfo.SpellLevel > 0;
        var hasEnemyInRange = HasVisibleEnemyInRange();
        return !isLearned || !hasEnemyInRange;
    }

    private void OnLaunchAttack(Spell spell) {
        if (spell?.CastInfo?.Targets == null || spell.CastInfo.Targets.Count == 0) {
            return;
        }

        var target = (spell.CastInfo.Targets[0] as CastTarget)?.Unit as AttackableUnit;
        if (target != null && IsValidTarget(_evelynn, target, HateSpikeFlags)) {
            _lastHateSpikeTarget = target;
        }
    }

    private void OnDealDamage(DamageData data) {
        if (data?.Target != null && IsValidTarget(_evelynn, data.Target, HateSpikeFlags)) {
            _lastHateSpikeTarget = data.Target;
        }
    }

    private bool HasVisibleEnemyInRange() {
        return GetVisibleEnemiesInRange().Count != 0;
    }

    private AttackableUnit SelectHateSpikeTarget() {
        var visibleTargets = GetVisibleEnemiesInRange();
        if (visibleTargets.Count == 0) {
            return null;
        }

        if (_lastHateSpikeTarget != null && IsVisibleTargetInRange(_lastHateSpikeTarget)) {
            return _lastHateSpikeTarget;
        }

        var championTargets = visibleTargets.Where(unit => unit is Champion).ToList();
        var targetPool = championTargets.Count != 0 ? championTargets : visibleTargets;

        return targetPool
            .OrderBy(GetHealthPercent)
            .ThenBy(unit => Vector2.DistanceSquared(_evelynn.Position, unit.Position))
            .FirstOrDefault();
    }

    private List<AttackableUnit> GetVisibleEnemiesInRange() {
        return GetUnitsInRange(_evelynn, _evelynn.Position, HateSpikeRange, true, HateSpikeFlags)
            .Where(IsVisibleTargetInRange)
            .ToList();
    }

    private bool IsVisibleTargetInRange(AttackableUnit target) {
        return target != null
               && IsValidTarget(_evelynn, target, HateSpikeFlags)
               && TeamHasVision(_evelynn.Team, target)
               && Vector2.DistanceSquared(_evelynn.Position, target.Position) <= HateSpikeRange * HateSpikeRange;
    }

    private static float GetHealthPercent(AttackableUnit target) {
        var maxHealth = target.Stats.HealthPoints.Total;
        if (maxHealth <= 0f) {
            return 1f;
        }

        return target.Stats.CurrentHealth / maxHealth;
    }

    public void OnSpellPostCast(Spell spell) {
    }
    
    private void OnUpdateStats(AttackableUnit target, float diff) {
        SetSpellToolTipVar(_evelynn,                         2, _evelynn.Stats.AttackDamage.Total * 0.5f + 0.05f * (_spell.CastInfo.SpellLevel -1), SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_evelynn,                         1, _evelynn.Stats.AbilityPower.Total * 0.35f + 0.05f * (_spell.CastInfo.SpellLevel -1), SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}

public class HateSpikeLineMissile : ISpellScript {
    private          ObjAIBase                       _evelynn;
    private readonly Dictionary<SpellMissile, float> _missileElapsed = new();
    private readonly HashSet<AttackableUnit>         _targetsHit     = [];

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Circle
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _evelynn = owner;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        _missileElapsed[missile] = 0f;
        ApiEventManager.OnSpellMissileUpdate.AddListener(this, missile, OnMissileUpdate);
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);
    }

    private void OnMissileUpdate(SpellMissile missile, float diff) {
        if (!_missileElapsed.TryGetValue(missile, out var elapsed)) {
            elapsed = 0f;
        }

        elapsed += diff;
        _missileElapsed[missile] = elapsed;

        if (elapsed < 40f) {
            return;
        }

        AddParticle(_evelynn, null, "Evelynn_Q_mis", missile.Position, size: 1f);
        _missileElapsed.Remove(missile);
    }

    private void OnMissileEnd(SpellMissile missile) {
        _missileElapsed.Remove(missile);
        ApiEventManager.OnSpellMissileUpdate.RemoveListener(this, missile, OnMissileUpdate);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _evelynn = owner;
        _targetsHit.Clear();
    }

    public void OnSpellPostCast(Spell spell) {
    }
    
    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (_targetsHit.Contains(target)) return;
        var mainSpell = _evelynn.GetSpell("EvelynnQ");
        AddParticleTarget(_evelynn, target, "Evelynn_Q_tar", target);
        var ap  = _evelynn.Stats.AbilityPower.Total * 0.35f + 0.05f * (mainSpell.CastInfo.SpellLevel - 1);
        var ad  = _evelynn.Stats.AttackDamage.Total * 0.5f  + 0.05f * (mainSpell.CastInfo.SpellLevel - 1);
        var dmg = 30f                                       + 15f + (mainSpell.CastInfo.SpellLevel - 1) + ap + ad;
        target.TakeDamage(_evelynn, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                          DamageResultType.RESULT_NORMAL);
        if (_evelynn.GetSpell("EvelynnW").CastInfo.SpellLevel > 0){
            AddBuff("EvelynnWPassive", 3f, 1, spell, _evelynn, _evelynn);
        }

        _targetsHit.Add(target);
    }
}
