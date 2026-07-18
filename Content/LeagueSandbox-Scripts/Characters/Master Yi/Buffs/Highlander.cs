using System;
using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class Highlander : IBuffGameScript {
    private ObjAIBase _masterYi;
    private Buff      _buff;
    private Spell       _spell;
    private Particle  _highlander;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.HASTE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _masterYi   = buff.SourceUnit;
        _buff       = buff;
        _spell = ownerSpell;

        _masterYi.SetAnimStates(new Dictionary<string, string> {
            { "Run", "Run_HASTE" }
        });
        
       var particleName = ownerSpell.CastInfo.SpellLevel switch {
            3 => "MasterYi_Base_R_Buf_Lvl3.troy",
            2 => "MasterYi_Base_R_Buf_Lvl2.troy",
            _ => "MasterYi_Base_R_Buf.troy"
        };
       
        _highlander = SpellEffectCreate(particleName,_masterYi, unit,  unit, lifetime: buff.Duration, flags: FXFlags.SimulateWhileOffScreen | FXFlags.PARDriven);

        StatsModifier.MoveSpeed.PercentBonus   = 0.25f + 0.1f * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.AttackSpeed.PercentBonus = 0.3f + 0.25f * (ownerSpell.CastInfo.SpellLevel - 1);
        unit.AddStatModifier(StatsModifier);
        ApiEventManager.OnAllowAddBuff.AddListener(this, _masterYi, OnAllowAddBuff);
        ApiEventManager.OnKill.AddListener(this, _masterYi, OnKill);
        ApiEventManager.OnAssist.AddListener(this, _masterYi, OnAssist);
    }

    private void OnKill(DeathData data) {
        ExtendDuration();
    }
    
    private void OnAssist(ObjAIBase assistant,DeathData data) {
        ExtendDuration();
    }

    private void ExtendDuration()
    {
        RemoveParticle(_highlander);
        var particleName = _spell.CastInfo.SpellLevel switch {
            3 => "MasterYi_Base_R_Buf_Lvl3.troy",
            2 => "MasterYi_Base_R_Buf_Lvl2.troy",
            _ => "MasterYi_Base_R_Buf.troy"
        };
        _highlander = SpellEffectCreate(particleName,_masterYi, _highlander,  _highlander, lifetime: _buff.Duration, flags: FXFlags.SimulateWhileOffScreen | FXFlags.PARDriven);
        SpellEffectCreate("MasterYi_Base_R_OnBuffKill.troy",_masterYi, _masterYi,  _masterYi, boneName: "C_Buffbone_Glb_Chest_Loc", flags: FXFlags.SimulateWhileOffScreen);
        var duration = (_buff.Duration - _buff.TimeElapsed) + 4f;
        AddBuff("Highlander", duration, 1, _spell, _masterYi, _masterYi);
    }

    private bool OnAllowAddBuff(AttackableUnit unit, AttackableUnit target, Buff buff) {
        if (buff.BuffType is not BuffType.SLOW) return true;
        Say(_masterYi, "game_lua_Highlander");
        return false;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_highlander);
        ApiEventManager.RemoveAllListenersForOwner(this);
        _masterYi.SetAnimStates(new Dictionary<string, string> {
            { "Run", "" }
        });
    }
}
