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
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class TurretShield : IBuffGameScript {
    private ObjAIBase     _turret;
    private AttackableUnit _unit;
    private Buff _buff;
    private Shield _vanguardAllyShield;
    private const float ShieldAmountPerSecond = 30f;
    private PeriodicTicker _periodicTicker;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _turret      = ownerSpell.CastInfo.Owner;
        _unit = unit;
        _buff        = buff;
        _vanguardAllyShield = AddShield(_turret, _unit, 30f);
    }

    public void OnUpdate(Buff buff, float diff)
    {
        if (_vanguardAllyShield.IsConsumed())
        {
            _buff.DeactivateBuff();
        }
        var ticks = _periodicTicker.ConsumeTicks(diff, 1000f, false, 1);
        if (ticks != 1) return;
        if (IsUnitInRange(_turret, _unit.Position, 1000f, true))
        {
            if (_vanguardAllyShield.Amount > 300f) return;
            IncShield(_vanguardAllyShield, ShieldAmountPerSecond);
        }
        else
        {
            ReduceShield(_vanguardAllyShield, ShieldAmountPerSecond);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveShield(_unit, _vanguardAllyShield);
    }
}