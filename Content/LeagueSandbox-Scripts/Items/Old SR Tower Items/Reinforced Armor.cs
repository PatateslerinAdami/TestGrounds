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

public class ItemID_1502 : IItemScript
{
    private ObjAIBase _turret;
    private bool _previousStateMinionsClose = false;
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner)
    {
        _turret = owner;
        StatsModifier.Armor.FlatBonus = 200f;
        StatsModifier.MagicResist.FlatBonus = 200f;
    }

    public void OnUpdate(float diff)
    {
        var enemyMinionsInRange = GetUnitsInRange(_turret, _turret.Position, 1000f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectMinions);
        var hasEnemyMinionsClose = enemyMinionsInRange.Count <= 0;
        if (hasEnemyMinionsClose == _previousStateMinionsClose) return;

        if (hasEnemyMinionsClose)
        {
            _turret.AddStatModifier(StatsModifier);
        }
        else
        {
            _turret.RemoveStatModifier(StatsModifier);
        }

        _previousStateMinionsClose = hasEnemyMinionsClose;
    }
}