using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class AatroxWPower : IBuffGameScript {
    private ObjAIBase _aatrox;
    private Spell     _spell;

    private Particle _weaponGlowParticle;
    private string   _weaponGlow, _weaponGlowR;
    
    
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _spell  = ownerspell;
        _aatrox = ownerspell?.CastInfo?.Owner;
        if (_aatrox == null) return;

        switch (_aatrox.SkinID) {
            case 1:
                _weaponGlow  = "Aatrox_Skin01_W_WeaponPower";
                _weaponGlowR = "Aatrox_Skin01_W_WeaponPowerR";
                break;
            case 2:
                _weaponGlow  = "Aatrox_Skin02_W_WeaponPower";
                _weaponGlowR = "Aatrox_Skin02_W_WeaponPowerR";
                break;
            default:
                _weaponGlow  = "Aatrox_Base_W_WeaponPower";
                _weaponGlowR = "Aatrox_Base_W_WeaponPowerR";
                break;
        }

        RemoveBuff(_aatrox, "AatroxWLife");
        _weaponGlowParticle = AddParticleTarget(_aatrox, _aatrox, _aatrox.HasBuff("AatroxR") ? _weaponGlowR : _weaponGlow, _aatrox, buff.Duration, bone: "weapon");
        ApiEventManager.OnAllowAddBuff.AddListener(this, _aatrox, OnAddBuff);
        RegisterCurrentRBuffEndListener();
    }

    private bool OnAddBuff(AttackableUnit applier, AttackableUnit target, Buff buff) {
        if (_aatrox == null || buff == null) return true;
        if (buff.Name is not "AatroxR") return true;
        RemoveParticle(_weaponGlowParticle);
        _weaponGlowParticle = AddParticleTarget(_aatrox, _aatrox, _weaponGlowR, _aatrox, -1f, bone: "weapon");
        ApiEventManager.OnBuffDeactivated.AddListener(this, buff, OnBuffEnd);
        return true;
    }

    private void OnBuffEnd(Buff buff) {
        if (_aatrox == null) return;
        RemoveParticle(_weaponGlowParticle);
        _weaponGlowParticle = AddParticleTarget(_aatrox, _aatrox, _weaponGlow, _aatrox, -1f, bone: "weapon");
        ApiEventManager.OnBuffDeactivated.RemoveListener(this);
    }

    private void RegisterCurrentRBuffEndListener() {
        if (_aatrox == null) return;
        var rBuff = _aatrox.GetBuffWithName("AatroxR");
        if (rBuff != null) {
            ApiEventManager.OnBuffDeactivated.AddListener(this, rBuff, OnBuffEnd);
        }
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_weaponGlowParticle);
        if (_aatrox == null || _spell == null) return;
        if (!_aatrox.HasBuff("AatroxWONHPowerBuff") || _aatrox.GetBuffsWithName("AatroxW").Count != 2) return;
        RemoveBuff(_aatrox,"AatroxWONHPowerBuff");
        AddBuff("AatroxWONHLifeBuff", 25000f, 1, _spell, _aatrox, _aatrox, true);
    }
    
}
