using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class JinxEMine : IBuffGameScript
{
    private ObjAIBase _jinx;
    private AttackableUnit _unit;
    private Buff _buff;
    private Spell _spell;
    private Particle _readyParticle;
    private PeriodicTicker _armingPeriodicTicker, _armedPeriodicTicker;
    private bool _isArmed;
    private bool _triggeredByUnit;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _jinx = ownerSpell.CastInfo.Owner;
        _unit = unit;
        _buff = buff;
        _spell = ownerSpell;
        //arming window.
        AddParticleTarget(_jinx, unit, "Jinx_E_Mine_Idle_Green", unit, buff.Duration, teamOnly: _jinx.Team);
        switch (_jinx.Team)
        {
            case TeamId.TEAM_BLUE:
                AddParticleTarget(_jinx, unit, "Jinx_E_Mine_Idle_Red", unit, buff.Duration,
                    teamOnly: TeamId.TEAM_PURPLE); break;
            case TeamId.TEAM_PURPLE:
                AddParticleTarget(_jinx, unit, "Jinx_E_Mine_Idle_Red", unit, buff.Duration,
                    teamOnly: TeamId.TEAM_BLUE); break;
        }

        PlayAnimation(unit, "Wait1", flags: AnimationFlags.Override);
        PauseAnimation(unit, true);
    }

    public void OnUpdate(float diff)
    {
        if (!_isArmed)
        {
            var armingTicks = _armingPeriodicTicker.ConsumeTicks(diff, 750f, false, 1, 1);
            if (armingTicks != 1) return;

            PauseAnimation(_unit, false);
            PlayAnimation(_unit, "Idle1", flags: AnimationFlags.Override);
            _readyParticle = AddParticleTarget(_jinx, _unit, "Jinx_E_Mine_Ready_Green", _unit, _buff.Duration, enemyParticle: "Jinx_E_Mine_Ready_Red");

            _isArmed = true;
            return;
        }

        var armedTicks = _armedPeriodicTicker.ConsumeTicks(diff, 5000f, false, 1, 1);
        if (armedTicks == 1)
        {
            _buff.DeactivateBuff();
            return;
        }

        LookForTarget();
    }

    private void LookForTarget()
    {
        var units = GetUnitsInRange(_unit, _unit.Position, 115, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes);
        if (units.Count == 0) return;
        foreach (var unit in units.OfType<AttackableUnit>().Where(unit => !unit.HasBuff("JinxEFireBurn"))
                     .Where(unit => !unit.HasBuff("JinxEMineLockout")))
        {
            AddParticleTarget(_jinx, _unit, "Jinx_E_Mine_Trigger_Sound", _unit, default);
            AddBuff("JinxEFireBurn", 1.5f, 1, _spell, unit, _jinx);
            AddBuff("JinxEMineSnare", 1.5f, 1, _spell, unit, _jinx);
            AddBuff("JinxEMineLockout", 5f, 1, _spell, unit, _jinx);
            _triggeredByUnit = true;
            _buff.DeactivateBuff();
            return;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        PauseAnimation(unit, false);
        RemoveParticle(_readyParticle);
        AddBuff("JinxEMineExplode", 0.75f, 1, ownerSpell, unit, _jinx);
        if (_triggeredByUnit)
        {
            return;
        }

        var units = GetUnitsInRange(_unit, _unit.Position, 125, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectNeutral);
        if (units.Count == 0) return;
        foreach (var unit1 in units.Where(unit1 => !unit1.HasBuff("JinxEFireBurn")).Where(unit1 => !unit1.HasBuff("JinxEMineLockout")))
        {
            AddBuff("JinxEFireBurn", 1.5f, 1, _spell, unit1, _jinx);
            AddBuff("JinxEMineLockout", 5f, 1, _spell, unit1, _jinx);
            return;
        }
    }
}