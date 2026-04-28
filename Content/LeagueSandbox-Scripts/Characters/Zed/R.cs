using System;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static GameServerCore.Content.HashFunctions;

namespace Spells;

public class ZedUlt : ISpellScript {
    private ObjAIBase      _zed;
    private AttackableUnit _target;
    private Spell          _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _zed   = owner;
        _spell = spell;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        SealSpellSlot(_zed, SpellSlotType.SpellSlots, 0, SpellbookType.SPELLBOOK_CHAMPION, true);
        SealSpellSlot(_zed, SpellSlotType.SpellSlots, 1, SpellbookType.SPELLBOOK_CHAMPION, true);
        SealSpellSlot(_zed, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, true);
        SealSpellSlot(_zed, SpellSlotType.SpellSlots, 3, SpellbookType.SPELLBOOK_CHAMPION, true);
        SealSpellSlot(_zed, SpellSlotType.SummonerSpellSlots, 0, SpellbookType.SPELLBOOK_SUMMONER, true);
        SealSpellSlot(_zed, SpellSlotType.SummonerSpellSlots, 1, SpellbookType.SPELLBOOK_SUMMONER, true);
        for (var i = 0; i <= 6; i++) {
            SealSpellSlot(_zed, SpellSlotType.InventorySlots, i, SpellbookType.SPELLBOOK_CHAMPION, true);
        }
        SealSpellSlot(_zed, SpellSlotType.BluePillSlot, (int)SpellSlotType.BluePillSlot,
                      SpellbookType.SPELLBOOK_CHAMPION, true);
        PlayAnimation(_zed, "Spell4");
        AddBuff("ZedUltDash", 2f, 1, spell, _target, _zed);
    }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell) {
        SealSpellSlot(_zed, SpellSlotType.SpellSlots,          1, SpellbookType.SPELLBOOK_CHAMPION, true);
        SealSpellSlot(_zed, SpellSlotType.SpellSlots,          3, SpellbookType.SPELLBOOK_CHAMPION, true);
        SealSpellSlot(_zed, SpellSlotType.SummonerSpellSlots, 0, SpellbookType.SPELLBOOK_SUMMONER, true);
        SealSpellSlot(_zed, SpellSlotType.SummonerSpellSlots, 1, SpellbookType.SPELLBOOK_SUMMONER, true);
        
        var zedR2 = _zed.GetSpell("ZedR2");
        zedR2?.SetCooldown(0f, true);
    }
}

public class ZedUltMissile : ISpellScript {
    private ObjAIBase _zed;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _zed   = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        LogInfo("Test");
    }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell) { _zed.SetStatus(StatusFlags.Targetable, true); }

    private void OnSpellHit(Spell spell, AttackableUnit unit, SpellMissile missile, SpellSector sector) {
        missile.SetToRemove();
    }
}

public class ZedR2 : ISpellScript {
    private ObjAIBase   _zed;
    private Spell       _spell;
    private ZedRHandler _zedRHandler;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _zed   = owner;
        _spell = spell;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var handlerBuff = owner.GetBuffWithName("ZedRHandler");
        if (handlerBuff == null) return;

        _zedRHandler = handlerBuff.BuffScript as ZedRHandler;
        if (_zedRHandler == null) return;
    }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell) {
        var handlerBuff = spell.CastInfo.Owner.GetBuffWithName("ZedRHandler");
        if (handlerBuff == null) return;
        _zedRHandler = handlerBuff.BuffScript as ZedRHandler;
        if (_zedRHandler == null) return;
        _zedRHandler.Swap();
    }
}
