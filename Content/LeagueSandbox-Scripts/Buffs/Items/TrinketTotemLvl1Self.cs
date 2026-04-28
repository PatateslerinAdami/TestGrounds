using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class TrinketTotemLvl1Self : IBuffGameScript {
    private float          _matchTimer = 1000f;
    private AttackableUnit _unit;
    private Buff           _buff;
    private bool           _enableStealthTimer = true;
    private PeriodicTicker _periodicTicker;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit = unit;
        _buff = buff;
    }

    public void OnUpdate(float diff) {
        if (_enableStealthTimer) {
            var ticks = _periodicTicker.ConsumeTicks(diff, 2000f, false, maxTotalTicks: 1);
            if (ticks == 1) {
                _unit.SetStatus(StatusFlags.Ghosted, true);
                _unit.SetIsTargetableToTeam(_unit.Team == TeamId.TEAM_BLUE ? TeamId.TEAM_PURPLE : TeamId.TEAM_BLUE, false);
                Fade _id        = PushCharacterFade(_unit, 0.2f, 0.2f);
                _enableStealthTimer = false;
            }
        }
        _matchTimer -= diff;
        if (!(_matchTimer <= 0)) return;
        _unit.Stats.CurrentMana = _unit.Stats.ManaPoints.Total - _buff.TimeElapsed;
        _matchTimer             = 1000f;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "Global_Trinket_Yellow_Death", unit);
        _unit.TakeDamage(_unit, 5000f, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_RAW, DamageResultType.RESULT_NORMAL);
    }
}