using System.Linq;
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


namespace ItemPassives;

public class ItemID_1504 : IItemScript
{
    private ObjAIBase _turret;
    private Shield _vanguardShield;
    private PeriodicTicker _periodicTicker, _periodicCombatTicker;
    private const float ShieldAmount = 200f;
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner)
    {
        _turret = owner;
        ApiEventManager.OnTakeDamage.AddListener(this, _turret, OnTakeDamage);
    }

    private void OnTakeDamage(DamageData data)
    {
        _periodicCombatTicker.Reset();
    }

    public void OnUpdate(float diff)
    {
        var ticks = _periodicTicker.ConsumeTicks(diff, 30000f, false, 1);
        if (ticks == 1 && _vanguardShield.Amount < ShieldAmount)
        {
            _vanguardShield = AddShield(_turret, _turret, ShieldAmount);
        }

        var ticks2 = _periodicTicker.ConsumeTicks(diff, 250f, true, 1);
        if (ticks2 != 1 && !_turret.HasShield(_vanguardShield)) return;
        var championsInRange = GetUnitsInRange(_turret, _turret.Position, 1000f, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectHeroes).Where(unit => !unit.HasBuff("TurretShield"));
        foreach (var champion in championsInRange)
        {
            AddBuff("TurretShield", 25000f, 1, _turret.AutoAttackSpell, champion, _turret);
        }
    }
}