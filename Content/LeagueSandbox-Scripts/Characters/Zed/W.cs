﻿using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class ZedShadowDash : ISpellScript {
    private ObjAIBase _zed;
    private Vector2   _endPosition;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _zed = owner;
        // This spell gets recreated during W/W2 swaps. Keep one persistent shadow tracker.
        if (_zed.GetBuffWithName("ZedShadowHandler") == null) {
            AddBuff("ZedShadowHandler", 10f, 1, spell, _zed, _zed, true);
        }
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        ApiEventManager.OnUpdateStats.AddListener(this, _zed, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _endPosition = end;

        var shadowTrackerBuff = _zed.GetBuffWithName("ZedShadowHandler");
        var zedShadowHandler  = shadowTrackerBuff?.BuffScript as ZedShadowHandler;
        zedShadowHandler?.RemoveWShadow();

        var wHandlerBuff = AddBuff("ZedWHandler", 4f, 1, spell, _zed, _zed, false);
        var zedWHandler  = wHandlerBuff?.BuffScript as ZedWHandler;
        zedWHandler?.SetSpellShadowSwap();
    }

    public void OnSpellCast(Spell spell) {
        PlayAnimation(_zed, "Spell2_Cast", 0.5f);
    }

    public void OnSpellPostCast(Spell spell) {
        SpellCast(_zed, 4, SpellSlotType.ExtraSlots, _endPosition, _endPosition, true, Vector2.Zero);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var bonusAd = _zed.Stats.AttackDamage.Total * 0.05f;
        SetSpellToolTipVar(_zed, 2, bonusAd, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }

    private void OnLevelUpSpell(Spell spell) {
        AddBuff("ZedWPassiveBuff", 1000000f, 1, spell, _zed, _zed, true);
    }
}

public class ZedShadowDashMissile : ISpellScript {
    private ObjAIBase _zed;
    private Spell     _spell;
    private ZedWHandler      _zedWHandler;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle,
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _zed   = owner;
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        ScriptMetadata.MissileParameters.OverrideEndPosition = end;
        var wHandlerBuff = _zed.GetBuffWithName("ZedWHandler");
        if (wHandlerBuff == null) {
            wHandlerBuff = AddBuff("ZedWHandler", 4f, 1, _spell, _zed, _zed, false);
        }
        _zedWHandler = wHandlerBuff?.BuffScript as ZedWHandler;
        _zedWHandler?.SetSpellShadowSwap();
        ApiEventManager.OnSpellCast.AddListener(this, _zed.GetSpell("ZedPBAOEDummy"), OnSpellCastSlash);
    }

    private void OnSpellCastSlash(Spell spell) {
        _zedWHandler?.QueueSlash();
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnSpellMissileEnd);
    }

    private void OnSpellMissileEnd(SpellMissile missile) {
        _zedWHandler.SpawnShadow(missile.Position);
        ApiEventManager.OnSpellCast.RemoveListener(this, _zed.GetSpell("ZedPBAOEDummy"));
    }
}

public class ZedW2 : ISpellScript {
    private ObjAIBase   _zed;
    private ZedWHandler _wShadowHandler;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
       TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _zed = owner;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var handlerBuff = owner.GetBuffWithName("ZedWHandler");
        if (handlerBuff == null) return;

        _wShadowHandler = handlerBuff.BuffScript as ZedWHandler;
        if (_wShadowHandler == null) return;
    }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell) {
        var handlerBuff = spell.CastInfo.Owner.GetBuffWithName("ZedWHandler");
        if (handlerBuff == null) return;
        _wShadowHandler = handlerBuff.BuffScript as ZedWHandler;
        _wShadowHandler?.Swap();
    }

    private void OnLevelUpSpell(Spell spell) {
        AddBuff("ZedWPassiveBuff", 1000000f, 1, spell, _zed, _zed, true);
    }
}
