using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using System;
using System.Collections.Generic;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SivirQ : ISpellScript
{
    private ObjAIBase _sivir;
    private Vector2 _start, _end;


    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
        IsDamagingSpell = true,
        SpellDamageRatio = 0.5f
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _sivir = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _start = start;
        _end = end;
    }

    public void OnSpellPostCast(Spell spell)
    {
        SpellCast(_sivir, 1, SpellSlotType.ExtraSlots, _start, _end, true, Vector2.Zero);
    }
}

public class SivirQMissile : ISpellScript
{
    private ObjAIBase _sivir;
    private Spell _spell;
    private HashSet<AttackableUnit> _hitUnits = [];

    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Arc
        },
        NotSingleTargetSpell = true,
        TriggersSpellCasts = false,
        CastingBreaksStealth = true,
        IsDamagingSpell = true,
        SpellDamageRatio = 0.5f,
        PersistsThroughDeath = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _sivir = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        AddParticleTarget(_sivir, target, "Sivir_base_Q_tar.troy", target, flags: FXFlags.SimulateWhileOffScreen);

        var ad = _sivir.Stats.AttackDamage.Total * _sivir.Spells[0].SpellData.Coefficient;
        var dmg = _sivir.Spells[0].SpellData.EffectLevelAmount[2][_sivir.Spells[0].CastInfo.SpellLevel] + ad;
        var modifier = System.Math.Max(_hitUnits.Count, 5) switch
        {
            1 => 0.15f,
            2 => 0.3f,
            3 => 0.45f,
            4 => 0.6f,
            _ => 0.0f
        };

        target.TakeDamage(_sivir, dmg - dmg * modifier, DamageType.DAMAGE_TYPE_PHYSICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);

        _hitUnits.Add(target);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnSpellMissileEnd);
    }

    private void OnSpellMissileEnd(SpellMissile missile)
    {
        if (_sivir.IsDead)
        {
            SpellCast(_sivir, 4, SpellSlotType.ExtraSlots, missile.Position, _sivir.Position, true, missile.Position);
        }
        else
        {
            SpellCast(_sivir, 3, SpellSlotType.ExtraSlots, true, _sivir, missile.Position,
                inheritVariablesFrom: _spell.CastInfo);
        }

        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnSpellMissileEnd);
        ApiEventManager.OnLaunchMissile.RemoveListener(this, _spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.RemoveListener(this, _spell, OnSpellHit);
    }
}

public class SivirQMissileReturn : ISpellScript
{
    private ObjAIBase _sivir;
    private Spell _spell;
    private HashSet<AttackableUnit> _hitUnits = [];

    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Arc
        },
        NotSingleTargetSpell = true,
        TriggersSpellCasts = false,
        CastingBreaksStealth = true,
        SpellDamageRatio = 0.5f
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _sivir = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnSpellMissileEnd);
        ApiEventManager.OnSpellMissileHit.AddListener(this, missile, OnSpellMissileHit);
    }

    private void OnSpellMissileEnd(SpellMissile missile)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    private void OnSpellMissileHit(SpellMissile missile, AttackableUnit target)
    {
        if (target != _sivir) return;
        ApiEventManager.RemoveAllListenersForOwner(this);
        missile.SetToRemove();
        _hitUnits.Clear();
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        AddParticleTarget(_sivir, target, "Sivir_base_Q_tar.troy", target, flags: FXFlags.SimulateWhileOffScreen);

        var ad = _sivir.Stats.AttackDamage.Total * _sivir.Spells[0].SpellData.Coefficient;
        var dmg = _sivir.Spells[0].SpellData.EffectLevelAmount[2][_sivir.Spells[0].CastInfo.SpellLevel] + ad;
        var modifier = System.Math.Max(_hitUnits.Count, 5) switch
        {
            1 => 0.15f,
            2 => 0.3f,
            3 => 0.45f,
            4 => 0.6f,
            _ => 0.0f
        };

        target.TakeDamage(_sivir, dmg - dmg * modifier, DamageType.DAMAGE_TYPE_PHYSICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);

        _hitUnits.Add(target);
    }
    
}

public class SivirQMissileReturnDead : ISpellScript
{
    private ObjAIBase _sivir;
    private Spell _spell;
    private HashSet<AttackableUnit> _hitUnits = [];
    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters()
        {
          Type = MissileType.Arc
        },
        NotSingleTargetSpell = true,
        TriggersSpellCasts = false,
        CastingBreaksStealth = true,
        IsDamagingSpell = true,
        SpellDamageRatio = 0.5f
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _sivir = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnSpellMissileEnd);
    }

    private void OnSpellMissileEnd(SpellMissile missile)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
        _hitUnits.Clear();
    }


    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        AddParticleTarget(_sivir, target, "Sivir_base_Q_tar.troy", target, flags: FXFlags.SimulateWhileOffScreen);

        var ad = _sivir.Stats.AttackDamage.Total * _sivir.Spells[0].SpellData.Coefficient;
        var dmg = _sivir.Spells[0].SpellData.EffectLevelAmount[2][_sivir.Spells[0].CastInfo.SpellLevel] + ad;
        var modifier = System.Math.Max(_hitUnits.Count, 5) switch
        {
            1 => 0.15f,
            2 => 0.3f,
            3 => 0.45f,
            4 => 0.6f,
            _ => 0.0f
        };

        target.TakeDamage(_sivir, dmg - dmg * modifier, DamageType.DAMAGE_TYPE_PHYSICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);

        _hitUnits.Add(target);
    }
}