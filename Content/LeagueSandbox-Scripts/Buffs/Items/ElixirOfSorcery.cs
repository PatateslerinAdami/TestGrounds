using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class ElixirOfSorcery : IBuffGameScript {
    private const float BonusTrueDamage           = 25f;
    private const float ChampionCooldownDurationMs = 5000f;

    private ObjAIBase      _owner;
    private bool           _isApplyingBonusDamage;
    private readonly Dictionary<uint, float> _championCooldownsMs = new();
    

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _owner                          = ownerSpell.CastInfo.Owner;
        StatsModifier.AbilityPower.FlatBonus     = 40f;
        StatsModifier.ManaRegeneration.FlatBonus = 15f;
        unit.AddStatModifier(StatsModifier);
        ApiEventManager.OnDealDamage.AddListener(this, _owner, OnDealDamage);
    }

    private void OnDealDamage(DamageData data) {
        if (_owner == null || data?.Target == null || _isApplyingBonusDamage) return;
        if (data.PostMitigationDamage <= 0f) return;

        var enemyChampion   = data.Target as Champion;
        var isEnemyChampion = enemyChampion != null &&
                              IsValidTarget(_owner, enemyChampion, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes);
        var isEnemyTurret   = IsValidTarget(_owner, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectTurrets);
        if (!isEnemyChampion && !isEnemyTurret) return;

        if (isEnemyChampion && _championCooldownsMs.TryGetValue(enemyChampion.NetId, out var cooldownRemaining) &&
            cooldownRemaining > 0f) {
            return;
        }

        if (isEnemyChampion) {
            _championCooldownsMs[enemyChampion.NetId] = ChampionCooldownDurationMs;
        }

        _isApplyingBonusDamage = true;
        try {
            data.Target.TakeDamage(
                _owner,
                BonusTrueDamage,
                DamageType.DAMAGE_TYPE_TRUE,
                DamageSource.DAMAGE_SOURCE_PROC,
                DamageResultType.RESULT_NORMAL
            );
        } finally {
            _isApplyingBonusDamage = false;
        }
    }

    public void OnUpdate(float diff) {
        if (_championCooldownsMs.Count == 0) return;

        var trackedChampions = new List<uint>(_championCooldownsMs.Keys);
        foreach (var championNetId in trackedChampions) {
            _championCooldownsMs[championNetId] -= diff;
        }

        var expiredCooldowns = new List<uint>();
        foreach (var cooldown in _championCooldownsMs) {
            if (cooldown.Value <= 0f) expiredCooldowns.Add(cooldown.Key);
        }

        foreach (var championNetId in expiredCooldowns) {
            _championCooldownsMs.Remove(championNetId);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        _championCooldownsMs.Clear();
    }
}
