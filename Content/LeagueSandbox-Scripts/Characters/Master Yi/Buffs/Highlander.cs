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
    private static readonly Random Rng = new();
    private ObjAIBase _masterYi;
    private Buff      _buff;
    private Spell       _spell;
    private Particle  _highlander;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _masterYi   = ownerSpell.CastInfo.Owner;
        _buff       = buff;
        _spell = ownerSpell;
        //this is for fun
        var runAnim = Rng.Next(0, 2) == 0 ? "Run_HASTE" : "2013_run_haste";
       var particleName = ownerSpell.CastInfo.SpellLevel switch {
            3 => "MasterYi_Base_R_Buf_Lvl3",
            2 => "MasterYi_Base_R_Buf_Lvl2",
            _ => "MasterYi_Base_R_Buf"
        };
       
        _masterYi.SetAnimStates(new Dictionary<string, string> {
            { "Run", runAnim }
        });
        
        _highlander = AddParticleTarget(_masterYi, unit, particleName, unit, -1f);

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
        AddParticleTarget(_masterYi, _masterYi, "MasterYi_Base_R_OnBuffKill", _masterYi);
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
