using Perfetto.Protos;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
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

namespace CharScripts;

public class CharScriptMasterYi : ICharScript
{
    private ObjAIBase _masterYi;
    private Particle _p1, _p2, _p3;
    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _masterYi = owner;
        AddBuff("MasterYiPassive", 25000f, 1, spell, owner, owner, true);
        if (_masterYi.SkinID != 2) return;
        ApiEventManager.OnUnitBuffActivated.AddListener(this, _masterYi, OnUnitBuffActivated);
        ApiEventManager.OnUnitBuffDeactivated.AddListener(this, _masterYi, OnUnitBuffDeactivated);
    }

    public void OnPostActivate(ObjAIBase owner, Spell spell = null)
    {
        if (_masterYi.SkinID != 2) return;
        _p1 = SpellEffectCreate("MasterYi_Skin02_Glow_Sword_Blue.troy", owner, owner, owner, lifetime: -1f,
            boneName: "BUFFBONE_Cstm_Sword1_loc",
            flags: FXFlags.SimulateWhileOffScreen | FXFlags.PARDriven | FXFlags.KeepAlive);
    }

    private void OnUnitBuffActivated(AttackableUnit unit, Buff buff)
    {
        switch (buff.Name)
        {
            case "WujuStyleVisual":
                RemoveParticle(_p1);
                _p2 = SpellEffectCreate("MasterYi_Skin02_Glow_Sword_Green.troy", _masterYi, _masterYi, _masterYi,
                    lifetime: -1f,
                    boneName: "BUFFBONE_Cstm_Sword1_loc",
                    flags: FXFlags.SimulateWhileOffScreen | FXFlags.PARDriven | FXFlags.KeepAlive);
                break;
            case "WujuStyleSuperChargedVisual":
                RemoveParticle(_p2);
                _p3 = SpellEffectCreate("MasterYi_Skin02_Glow_Sword_Red.troy", _masterYi, _masterYi, _masterYi, lifetime: buff.Duration, boneName: "BUFFBONE_Cstm_Sword1_loc",
                    flags: FXFlags.SimulateWhileOffScreen | FXFlags.PARDriven | FXFlags.KeepAlive);
                break;
        }
    }
    private void OnUnitBuffDeactivated(AttackableUnit unit, Buff buff)
    {
        if (!buff.Name.Equals("WujuStyleSuperChargedVisual")) return;
        RemoveParticle(_p3);
        _p1 = SpellEffectCreate("MasterYi_Skin02_Glow_Sword_Blue.troy", _masterYi, _masterYi, _masterYi, lifetime: -1f,
            boneName: "BUFFBONE_Cstm_Sword1_loc",
            flags: FXFlags.SimulateWhileOffScreen | FXFlags.PARDriven | FXFlags.KeepAlive);
    }
}