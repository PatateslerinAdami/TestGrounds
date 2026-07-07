using System.Numerics;
using System.Threading;
using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class NoxiousTrap : IBuffGameScript {
    private ObjAIBase      _teemo;
    private AttackableUnit _unit;
    private Spell          _spell;
    private Buff           _buff;
    private PeriodicTicker _resourcePeriodicTicker;
    private PeriodicTicker _armingPeriodicTicker;
    private bool           _isArming = true;


    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _spell = ownerSpell;
        _buff  = buff;
        _unit  = unit;
        _teemo = ownerSpell.CastInfo.Owner;
    }

    public void OnUpdate(float diff) {
        var ticks = _resourcePeriodicTicker.ConsumeTicks(diff, 1000f, false, 1, (int) _unit.Stats.ManaPoints.Total);
        if (ticks == 1) { _unit.Stats.CurrentMana = _unit.Stats.ManaPoints.Total - _buff.TimeElapsed; }

        if (_isArming) {
            var armingTicks = _armingPeriodicTicker.ConsumeTicks(diff, 1000f, false, 1, 1);
            if (armingTicks == 1) { _isArming = false; }
        } else
        {
            var unitsInRange = GetUnitsInRange(_teemo, _unit.Position, 160f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectNeutral);
            if (unitsInRange.Count == 0) return;
            foreach (var unit in unitsInRange) {
                AddBuff("BantamTrap", 4f, 1, _spell, unit, _teemo);
                var variables = new VariableTable();
                variables.Set("slowPercent", 0.3f + 0.1f * (_spell.CastInfo.SpellLevel - 1));
                AddBuff("Slow", 4f, 1, _spell, unit, _teemo, variableTable: variables);
            }

            SetVisible();
            _buff.DeactivateBuff();
        }
    }

    private void SetVisible() {
        PushCharacterFade(_unit, 1f, 0.75f);
        _unit.SetStatus(StatusFlags.Stealthed, false);
        _unit.SetVisibleByTeam(TeamId.TEAM_BLUE,   true);
        _unit.SetVisibleByTeam(TeamId.TEAM_PURPLE, true);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit.TakeDamage(_unit, 4000f, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_RAW,
                         DamageResultType.RESULT_NORMAL);
    }
}