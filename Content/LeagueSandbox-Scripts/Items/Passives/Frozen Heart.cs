using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemId3110 : IItemScript {
    private ObjAIBase     _owner;
    public  StatsModifier StatsModifier { get; } = new();
    private PeriodicTicker _periodicTicker;

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
    }

    public void OnUpdate(float diff) {
        var ticks = _periodicTicker.ConsumeTicks(diff, 250f, true, 1);
        if (ticks != 1) return;
        var enemiesInRange = GetUnitsInRange(_owner, _owner.Position, 700f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes);
        foreach (var enemy in enemiesInRange) {
            AddBuff("FrozenHeartAura",  2f, 1, _owner.AutoAttackSpell, enemy, _owner);
        }
    }
}