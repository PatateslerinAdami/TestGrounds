using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
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
    private Particle _passiveParticle;

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
                _passiveParticle = AddParticleTarget(_soraka, _soraka,
                    "soraka_base_passive_speed.troy", _soraka, lifetime: float.MaxValue);
            } else {
                _soraka.RemoveStatModifier(StatsModifier);
                if (_passiveParticle != null) {
                    RemoveParticle(_passiveParticle);
                    _passiveParticle = null;
                }
            }
        }

        // Update target indicator — arrow pointing to nearest low-HP ally
        if (shouldBeActive && validAlly != null)
        {
            if (_passiveParticle != null)
            {
                AddParticleTarget(_soraka, validAlly,
                    "soraka_base_passive_cross.troy", validAlly, lifetime: 0.1f);
            }
            // Arrow indicator pointing from Soraka toward the ally
            AddParticlePos(_soraka, "soraka_base_passive_indicatior.troy",
                _soraka.Position, validAlly.Position,
                lifetime: 0.25f, direction: _soraka.Direction);
        }
    }

    public void OnPostActivate(ObjAIBase owner, Spell spell = null) { }
}
