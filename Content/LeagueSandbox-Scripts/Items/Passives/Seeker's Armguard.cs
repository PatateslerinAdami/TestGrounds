using System.Collections.Concurrent;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;

namespace ItemPassives;

public class ItemID_3191 : IItemScript {
    private static readonly ConcurrentDictionary<ObjAIBase, float> CachedArmor = new();
    private                 float                                  _armor;
    public                  StatsModifier                          StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        if (CachedArmor.TryRemove(owner, out var value)) {
            owner.Stats.Armor.FlatBonus += value;
            LoggerProvider.GetLogger().Info(_armor);
            _armor = value;
        }

        ApiEventManager.OnKillUnit.AddListener(this, owner, TargetExecute);
    }

    public void OnDeactivate(ObjAIBase owner) {
        ApiEventManager.OnKillUnit.RemoveListener(this);
        CachedArmor.TryAdd(owner, _armor);
        LoggerProvider.GetLogger().Info(_armor);
        owner.Stats.Armor.FlatBonus -= _armor;
    }

    public void TargetExecute(DeathData deathData) {
        if (_armor > 30f) return;
        deathData.Killer.Stats.Armor.FlatBonus += 0.5f;
        //Add tooltip
        _armor += 0.5f;
    }
}