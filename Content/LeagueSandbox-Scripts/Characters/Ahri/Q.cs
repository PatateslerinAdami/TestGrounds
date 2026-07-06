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

public class AhriOrbofDeception : ISpellScript
{
    private ObjAIBase _ahri;
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
        _ahri = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var ap = _ahri.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var dmg = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;

        target.TakeDamage(_ahri, dmg, DamageType.DAMAGE_TYPE_MAGICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _start = start;
        _end = end;
    }

    public void OnSpellPostCast(Spell spell)
    {
        SpellCast(_ahri, 0, SpellSlotType.ExtraSlots, _start, _end, true, Vector2.Zero);
    }
}

public class AhriOrbMissile : ISpellScript
{
    private ObjAIBase _ahri;
    private Spell _spell;

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
        _ahri = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        _ahri.Spells[0].ApplyEffects(target);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnSpellMissileEnd);
    }

    private void OnSpellMissileEnd(SpellMissile missile)
    {
        if (_ahri.IsDead)
        {
            SpellCast(_ahri, 6, SpellSlotType.ExtraSlots, missile.Position, _ahri.Position, true, missile.Position);
        }
        else
        {
            SpellCast(_ahri, 1, SpellSlotType.ExtraSlots, true, _ahri, missile.Position);
        }

        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnSpellMissileEnd);
        ApiEventManager.OnLaunchMissile.RemoveListener(this, _spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.RemoveListener(this, _spell, OnSpellHit);
    }
}

public class AhriOrbReturn : ISpellScript
{
    private ObjAIBase _ahri;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Arc
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ahri = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
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
        if (target != _ahri) return;
        ApiEventManager.RemoveAllListenersForOwner(this);
        missile.SetToRemove();
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        _ahri.Spells[0].ApplyEffects(target);
    }
    
}

public class AhriOrbReturnDead : ISpellScript
{
    private ObjAIBase _ahri;
    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters()
        {
          Type = MissileType.Arc
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ahri = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
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
    }


    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        _ahri.Spells[0].ApplyEffects(target);
    }
}