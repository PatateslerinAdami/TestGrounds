using System.Numerics;
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

public class AatroxWLife : IBuffGameScript {
    private ObjAIBase _aatrox;
    private Spell     _spell;
    private Particle  _weaponGlow;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _aatrox = ownerspell.CastInfo.Owner;
        _spell  = ownerspell;
        RemoveBuff(_aatrox, "AatroxWPower");
        _weaponGlow = AddParticleTarget(_aatrox, _aatrox, _aatrox.HasBuff("AatroxR") ? "Aatrox_Base_W_WeaponLifeR" : "Aatrox_Base_W_WeaponLife", _aatrox, -1f, bone: "weapon");
        ApiEventManager.OnAllowAddBuff.AddListener(this, _aatrox, OnAddBuff);
        RegisterCurrentRBuffEndListener();
    }

    private bool OnAddBuff(AttackableUnit applier, AttackableUnit target, Buff buff) {
        if (buff.Name is not "AatroxR") return true;
        _weaponGlow.SetToRemove();
        _weaponGlow = AddParticleTarget(_aatrox, _aatrox, "Aatrox_Base_W_WeaponLifeR", _aatrox, -1f, bone: "weapon");
        ApiEventManager.OnBuffDeactivated.AddListener(this, buff, OnBuffEnd);
        return true;
    }
    
    private void OnBuffEnd(Buff buff) {
        RemoveParticle(_weaponGlow);
        _weaponGlow = AddParticleTarget(_aatrox, _aatrox, "Aatrox_Base_W_WeaponLife", _aatrox, -1f, bone: "weapon");
        ApiEventManager.OnBuffDeactivated.RemoveListener(this);
    }

    private void RegisterCurrentRBuffEndListener() {
        var rBuff = _aatrox.GetBuffWithName("AatroxR");
        if (rBuff != null) {
            ApiEventManager.OnBuffDeactivated.AddListener(this, rBuff, OnBuffEnd);
        }
    }

    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_weaponGlow);
        if (!_aatrox.HasBuff("AatroxWONHLifeBuff") || _aatrox.GetBuffsWithName("AatroxW").Count != 2) return;
        RemoveBuff(_aatrox,"AatroxWONHLifeBuff");
        AddBuff("AatroxWONHPowerBuff", 25000f, 1, _spell, _aatrox, _aatrox, true);
    }
}
