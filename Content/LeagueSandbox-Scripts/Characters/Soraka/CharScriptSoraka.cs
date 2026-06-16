using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

/// <summary>
/// Soraka passive — Salvation/Consecration (4.17+).
/// Gains 70% bonus movement speed when moving toward allied champions below 40% HP
/// who are outside of Astral Infusion's cast range (550).
/// Range: 2500
/// </summary>
public class CharScriptSoraka : ICharScript {
    private const float PassiveRange = 2500f;
    private const float WRange = 550f;
    private const float HpThreshold = 0.40f;
    private const float MsPercentBonus = 0.70f;

    private ObjAIBase _soraka;
    private bool _passiveActive;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _soraka = owner;
    }

    public void OnUpdate(float diff) {
        if (_soraka == null || _soraka.IsDead) return;

        // Check if any nearby ally is below 40% HP AND outside W range (550)
        var allies = GetChampionsInRange(_soraka, _soraka.Position, PassiveRange, false,
            getAllies: true, getEnemies: false);

        var validAlly = allies
            .Where(c => c != _soraka
                && c.Stats.CurrentHealth / c.Stats.HealthPoints.Total < HpThreshold
                && Vector2.Distance(_soraka.Position, c.Position) > WRange)
            .OrderBy(c => c.Stats.CurrentHealth / c.Stats.HealthPoints.Total)
            .FirstOrDefault();

        bool shouldBeActive = validAlly != null;

        if (shouldBeActive != _passiveActive) {
            _passiveActive = shouldBeActive;
            if (shouldBeActive) {
                StatsModifier.MoveSpeed.PercentBonus = MsPercentBonus;
                _soraka.AddStatModifier(StatsModifier);
            } else {
                _soraka.RemoveStatModifier(StatsModifier);
            }
        }
    }

    public void OnPostActivate(ObjAIBase owner, Spell spell = null) { }
}
