using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class IreliaTranscendentBlades : IBuffGameScript {
    private ObjAIBase       _irelia;
    private Particle        _p;
    private Particle        _p1;
    private Particle        _p2;
    private Particle        _p3;
    private Particle        _p4;
    private int           _blades = 4;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INTERNAL,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 1
    };
    
    public StatsModifier StatsModifier { get; } = new();

   public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
       _irelia = ownerSpell.CastInfo.Owner;
       
       _irelia.GetSpell("IreliaTranscendentBlades").CastInfo.ManaCost = 100;
       ApiEventManager.OnSpellCast.AddListener(this, _irelia.GetSpell("IreliaTranscendentBlades"), OnSpellCastUlt);
       _p = AddParticleTarget(_irelia, _irelia, "irelia_ult_magic_resist.troy", _irelia, 10f);
       _p1 = AddParticleTarget(_irelia, _irelia, "Irelia_ult_dagger_active_04.troy", _irelia, 10f, bone: "BUFFBONE_CSTM_DAGGER1");
       _p2 = AddParticleTarget(_irelia, _irelia, "Irelia_ult_dagger_active_04.troy", _irelia, 10f, bone: "BUFFBONE_CSTM_DAGGER2");
       _p3 = AddParticleTarget(_irelia, _irelia, "Irelia_ult_dagger_active_04.troy", _irelia, 10f, bone: "BUFFBONE_CSTM_DAGGER4");
       _p4 = AddParticleTarget(_irelia, _irelia, "Irelia_ult_dagger_active_04.troy", _irelia, 10f, bone: "BUFFBONE_CSTM_DAGGER5");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p);
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
        RemoveParticle(_p4);
        _irelia.GetSpell("IreliaTranscendentBlades").CastInfo.ManaCost = 0;
        _irelia.GetSpell("IreliaTranscendentBlades").SetCooldown(_irelia.GetSpell("IreliaTranscendentBlades").CastInfo.Cooldown, false);
        ApiEventManager.OnSpellCast.RemoveListener(this, _irelia.GetSpell("IreliaTranscendentBlades"), OnSpellCastUlt);
    }

    public void OnSpellCastUlt(Spell spell) {
        if (_blades <= 0) return;
        
        switch (_blades) {
            case 1:
                RemoveParticle(_p1);
                break;
            case 2: 
                RemoveParticle(_p3);
                break;
            case 3: 
                RemoveParticle(_p2);
                break;
            case 4: 
                RemoveParticle(_p4);
                break;
        }
        
        _blades -= 1;
    }
}