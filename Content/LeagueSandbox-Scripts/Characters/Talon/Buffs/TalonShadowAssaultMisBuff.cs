using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using System.Collections.Generic;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Buffs;

internal class TalonShadowAssaultMisBuff : IBuffGameScript
{
    private ObjAIBase _talon;
    private Buff _buff;
    private AttackableUnit _unit;
    private Particle _p1;
    private const int BladeCount = 8;
    private int _bladesAdded = 0;
    private Vector2[] _positions = new Vector2[BladeCount];
    private readonly Particle[] _neutral = new Particle[BladeCount];
    private readonly Particle[] _teamBased = new Particle[BladeCount];

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _buff = buff;
        _talon = buff.SourceUnit;
        _unit = unit;
        _positions = buff.BuffVars.Get("positions", new Vector2[BladeCount]);
        ApiEventManager.OnLaunchMissile.AddListener(this, _talon.GetSpell("TalonShadowAssaultMisOne"), OnLaunchMissile);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd);
    }

    private void OnMissileEnd(SpellMissile missile)
    {
        for (var i = 0; i < _positions.Length; i++)
        {
            if (_positions[i] != missile.Position) continue;
            _neutral[i] = AddParticlePos(_talon, "talon_ult_blade_hold", _positions[i], _positions[i], _buff.Duration,
                bone: "root");
            _teamBased[i] = AddParticlePos(_talon, "talon_ult_blade_hold_team_ID_green", _positions[i], _positions[i],
                _buff.Duration, enemyParticle: "talon_ult_blade_hold_team_ID_red");
        }
    }


    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
        for (var i = 0; i < BladeCount; i++)
        {
            RemoveParticle(_neutral[i]);
            RemoveParticle(_teamBased[i]);
        }
        
        ownerSpell.CastInfo.InstanceVars.Set("hitOutgoing", new HashSet<AttackableUnit>());
        for (var i = 0; i < BladeCount; i++)
        {
            SpellCast(_talon, 4, SpellSlotType.ExtraSlots, true, _talon, _positions[i], inheritVariablesFrom: ownerSpell.CastInfo);
        }
    }
}