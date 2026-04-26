using System.Collections.Concurrent;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;

namespace ItemPassives;

public class ItemID_3206 : IItemScript {
    private static readonly ConcurrentDictionary<ObjAIBase, float> CachedAbilityPower = new();
    private                 float                                  _ap;
    public                  StatsModifier                          StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        if (CachedAbilityPower.TryRemove(owner, out var value)) {
            owner.Stats.AbilityPower.FlatBonus += value;
            LoggerProvider.GetLogger().Info(_ap);
            _ap = value;
        }

        ApiEventManager.OnKillUnit.AddListener(this, owner, TargetExecute);
    }

    public void OnDeactivate(ObjAIBase owner) {
        ApiEventManager.OnKillUnit.RemoveListener(this);
        CachedAbilityPower.TryAdd(owner, _ap);
        LoggerProvider.GetLogger().Info(_ap);
        owner.Stats.Armor.FlatBonus -= _ap;
    }

    public void TargetExecute(DeathData deathData) {
        LoggerProvider.GetLogger().Info(deathData.Unit.CharData.UnitTags);
        if (deathData.Unit.CharData.UnitTags is UnitTag.Champion) {
            deathData.Killer.Stats.AbilityPower.FlatBonus += 2f;
            _ap                                           += 2f;
        }
    }
}