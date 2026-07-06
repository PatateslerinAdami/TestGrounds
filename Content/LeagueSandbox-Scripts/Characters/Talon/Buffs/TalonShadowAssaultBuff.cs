using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class TalonShadowAssaultBuff : IBuffGameScript {
    private ObjAIBase              _talon;
    private Buff           _buff;
    private AttackableUnit _unit;
    private Particle       _p1, _p2;
    private Fade           _id;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff  = buff;
        _talon = ownerSpell.CastInfo.Owner;
        _unit  = unit;
        
        ApiEventManager.OnSpellCast.AddListener(this, _talon.GetSpell("TalonRake"),            OnBreakCast);
        ApiEventManager.OnSpellCast.AddListener(this, _talon.GetSpell("TalonCutthroat"),       OnBreakCast);
        ApiEventManager.OnLaunchAttack.AddListener(this, _talon, OnBreakCast);
        
        SetStatus(_talon, StatusFlags.RevealSpecificUnit, false);
        SetStatus(_talon, StatusFlags.Stealthed, true);
        SetStatus(_talon, StatusFlags.Ghosted,   true);
        
        StatsModifier.MoveSpeed.PercentBonus += 0.4f;
        _unit.AddStatModifier(StatsModifier);
        
        _p1 = AddParticleTarget(_talon, _talon, "talon_invis_cas", _talon, buff.Duration);
        _p2 = AddParticleTarget(_talon, _talon, "global_haste", _talon, buff.Duration);
        _id = PushCharacterFade(_talon, 0.4f, 0.015f);
    }
    
    private void OnBreakCast(Spell spell) {
        RemoveBuff(_talon,"TalonShadowAssaultBuff");
        /*_bladeTimer  = 0f;
        _returnTimer = 0f;
        for (var i = 0; i < BladeCount; i++) {
            RemoveParticle(_neutral[i]);
            RemoveParticle(_teamBased[i]);
        }*/
    }


    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        
        RemoveBuff(_talon, "TalonShadowAssaultMisBuff");
        
        PushCharacterFade(_talon, 1f, 0.1f);
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        
        SetStatus(_talon, StatusFlags.Stealthed, false);
        SetStatus(_talon, StatusFlags.Ghosted,   false);
        
        SetSpell(_talon, "TalonShadowAssault", SpellSlotType.SpellSlots, 3);
        ownerSpell.SetCooldown(ownerSpell.CastInfo.Cooldown, false);
    }
}