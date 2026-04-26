using System;
using System.Linq;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptIrelia : ICharScript {
    private const float NearbyEnemyRange      = 1400f;
    private const float PassiveDurationSeconds = 250000f;
    private const float PassiveParticleLifetime = 100000000f;
    private const string PassiveBuffName = "IreliaIonianDuelist";
    private const string MantleBone      = "BUFFBONE_CSTM_BACK_2";

    private static readonly float[] TenacityBonus = [0.0f, 0.10f, 0.25f, 0.40f];
    private static readonly float[] TooltipTenacityPercent = [0.0f, 10.0f, 25.0f, 40.0f];

    private readonly StatsModifier _tenacityModifier = new();
    private ObjAIBase _irelia;
    private Spell     _spell;
    private int       _lastNearbyCount = -1;
    private Particle  _p1;
    private Particle  _p2;
    private Particle  _p3;
    private Particle  _p4;
    private Particle  _p5;

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _irelia = owner;
        _spell  = spell;
        UpdatePassiveState(forceRefresh: true);
    }

    public void OnUpdate(float diff) {
        UpdatePassiveState(forceRefresh: false);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell = null) {
        if (_irelia == null) {
            return;
        }

        _irelia.RemoveStatModifier(_tenacityModifier);
        RemoveParticles();

        foreach (var passiveBuff in _irelia.GetBuffsWithName(PassiveBuffName).ToList().Where(passiveBuff => _irelia.HasBuff(passiveBuff))) { _irelia.RemoveBuff(passiveBuff); }
    }

    private void UpdatePassiveState(bool forceRefresh) {
        if (_irelia == null) {
            return;
        }

        var nearbyEnemyCount = GetNearbyEnemyCount();
        if (!forceRefresh && nearbyEnemyCount == _lastNearbyCount) {
            EnsurePassiveVisualBuff(nearbyEnemyCount, forceRebuild: false);
            return;
        }

        _lastNearbyCount = nearbyEnemyCount;

        ApplyTenacity(nearbyEnemyCount);
        UpdateParticles(nearbyEnemyCount);
        EnsurePassiveVisualBuff(nearbyEnemyCount, forceRebuild: true);
    }

    private int GetNearbyEnemyCount() {
        if (_irelia.IsDead) {
            return 0;
        }

        var nearbyEnemyChampions = GetChampionsInRange(_irelia.Position, NearbyEnemyRange, true)
            .Count(unit => unit is Champion champion
                           && champion != _irelia
                           && champion.Team != _irelia.Team
                           && !champion.IsDead);

        return Math.Clamp(nearbyEnemyChampions, 0, 3);
    }

    private void ApplyTenacity(int nearbyEnemyCount) {
        _irelia.RemoveStatModifier(_tenacityModifier);

        var tenacityBonus = TenacityBonus[nearbyEnemyCount];
        if (tenacityBonus <= 0.0f) {
            return;
        }

        _tenacityModifier.Tenacity.FlatBonus = tenacityBonus;
        _irelia.AddStatModifier(_tenacityModifier);
    }

    private void EnsurePassiveVisualBuff(int nearbyEnemyCount, bool forceRebuild) {
        var existingBuffs = _irelia.GetBuffsWithName(PassiveBuffName).ToList();
        var existingStacks = existingBuffs.Count > 0 ? existingBuffs[0].StackCount : 0;

        if (!forceRebuild && existingStacks == nearbyEnemyCount) {
            var currentBuff = _irelia.GetBuffWithName(PassiveBuffName);
            currentBuff?.SetToolTipVar(0, TooltipTenacityPercent[nearbyEnemyCount]);
            currentBuff?.SetToolTipVar(1, TooltipTenacityPercent[nearbyEnemyCount]);
            return;
        }
        
        foreach (var buff in existingBuffs.Where(buff => _irelia.HasBuff(buff))) { _irelia.RemoveBuff(buff); }

        if (nearbyEnemyCount == 0) {
            return;
        }

        for (var i = 0; i < nearbyEnemyCount; i++) {
            AddBuff(PassiveBuffName, PassiveDurationSeconds, 1, _spell, _irelia, _irelia, true);
        }

        var passiveBuff = _irelia.GetBuffWithName(PassiveBuffName);
        passiveBuff?.SetToolTipVar(0, TooltipTenacityPercent[nearbyEnemyCount]);
        passiveBuff?.SetToolTipVar(1, TooltipTenacityPercent[nearbyEnemyCount]);
    }

    private void UpdateParticles(int currentCount) {
        RemoveParticles();
        switch (currentCount) {
            case 1:
                _p1 = AddParticleTarget(_irelia, _irelia, "irelia_new_passive_01.troy", _irelia, bone: MantleBone,
                                        lifetime: PassiveParticleLifetime);
                break;
            case 2:
            case >= 3:
                _p1 = AddParticleTarget(_irelia, _irelia, "irelia_new_passive_01.troy", _irelia, bone: MantleBone,
                                        lifetime: PassiveParticleLifetime);
                _p2 = AddParticleTarget(_irelia, _irelia, "irelia_new_passive_02.troy", _irelia, bone: MantleBone,
                                        lifetime: PassiveParticleLifetime);
                break;
        }

        _p4 = AddParticleTarget(_irelia, _irelia, "irelia_new_passive_empty.troy", _irelia, bone: MantleBone,
                                lifetime: PassiveParticleLifetime);
    }

    private void RemoveParticles() {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
        RemoveParticle(_p4);
        RemoveParticle(_p5);

        _p1 = null;
        _p2 = null;
        _p3 = null;
        _p4 = null;
        _p5 = null;
    }
}
