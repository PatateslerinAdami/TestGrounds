using System.Collections.Generic;
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

internal class AatroxR : IBuffGameScript {
    private       ObjAIBase _aatrox;
    private       Particle  _flashEffectParticle;
    private       string    _flashEffect;
    private const string    AutoAttack4 = "AatroxBasicAttack4"; //good
    private const string    AutoAttack5 = "AatroxBasicAttack5"; //good

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _aatrox = ownerSpell.CastInfo.Owner;
        _flashEffect = _aatrox.SkinID switch {
            0 => "Aatrox_Base_RModel",
            1 => "Aatrox_Skin01_RModel",
            2 => "Aatrox_Skin02_RModel",
            _ => _flashEffect
        };
        RemoveParticle(_flashEffectParticle);
        // flash effect 
        //_flashEffectParticle = AddParticleTarget(_aatrox, _aatrox, _flashEffect, _aatrox, lifetime: buff.Duration);
        //AddParticleTarget(_aatrox, _aatrox, "Aatrox_Base_R_decal", _aatrox, buff.Duration, bone: "weapon");
        
        _aatrox.SetAnimStates(new Dictionary<string, string> {
            { "attack1", "Attack1_ULT" },
            { "attack2", "Attack2_ULT" },
            { "attack3", "Attack6" },
            { "Crit", "Crit2" },
            { "Run", "RUN_ULT" },
            { "Run_IN", "Run_ULT_IN_BASE" },
            { "Run_BASE", "Run_ULT_BASE" },
            { "Run2", "RUN_ULT" },
            { "Run2_IN", "Run_ULT_IN_BASE" },
            { "Run2_BASE", "Run_ULT_BASE" },
            { "Run_Haste", "RUN_ULT" },
            { "Run_Haste_IN", "Run_ULT_IN_BASE" },
            { "Idle_Sel", "Idle_ULT" },
            { "idle1", "Idle_ULT" },
            { "Idle_in", "Idle_ULT_IN" },
            { "Idle_IN_BASE", "Idle_ULT_IN" },
            { "Idle1_BASE", "Idle_ULT_BASE" },
            { "Idle2_Base", "Idle_ULT_BASE" },
            { "Idle3_BASE", "Idle_ULT_BASE" },
            { "Spell2", "spell2_ULT" },
            { "Spell3", "Spell3_ULT" }
        });

        StatsModifier.AttackSpeed.PercentBonus = 0.4f + 0.1f * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.Range.FlatBonus          = 175f;
        unit.AddStatModifier(StatsModifier);

        _aatrox.SetAutoAttackSpells(false, AutoAttack4, AutoAttack5);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_flashEffectParticle);
        unit.SetAnimStates(new Dictionary<string, string> {
            { "attack1", "" },
            { "attack2", "" },
            { "attack3", "" },
            { "Crit", "" },
            { "Run", "" },
            { "Run_IN", "" },
            { "Run_BASE", "" },
            { "Run2", "" },
            { "Run2_IN", "" },
            { "Run2_BASE", "" },
            { "Run_Haste", "" },
            { "Run_Haste_IN", "" },
            { "Idle_Sel", "" },
            { "idle1", "" },
            { "Idle_in", "" },
            { "Idle_IN_BASE", "" },
            { "Idle1_BASE", "" },
            { "Idle2_Base", "" },
            { "Idle3_BASE", "" },
            { "Spell2", "" },
            { "Spell3", "" }
        });
        PlayAnimation(_aatrox, "Spell4End");
        _aatrox.ResetAutoAttackSpell();
    }

    public void OnUpdate(float diff) { }
}