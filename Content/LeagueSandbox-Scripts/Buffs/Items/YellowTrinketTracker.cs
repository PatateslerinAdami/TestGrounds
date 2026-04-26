using System.Collections.Generic;
using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
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

internal class YellowTrinketTracker : IBuffGameScript {
    private          ObjAIBase    _owner;
    private readonly List<Minion> _wards = [];
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _owner = ownerSpell.CastInfo.Owner;
    }

    public int GetWardCount() {
        return _wards.Count;
    }

    public void AddWard(Minion ward) {
        if (_wards.Count >= 3) {
            _wards.First().Stats.CurrentMana = 0;
        }
        _wards.Add(ward);
        ApiEventManager.OnDeath.AddListener(this, ward, OnDeath);
    }

    private void OnDeath(DeathData data) {
        _wards.Remove(data.Unit as Minion);
        ApiEventManager.OnDeath.RemoveListener(this, data.Unit, OnDeath);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}