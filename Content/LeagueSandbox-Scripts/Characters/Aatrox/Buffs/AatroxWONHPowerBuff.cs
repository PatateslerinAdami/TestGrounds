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

public class AatroxWONHPowerBuff  : IBuffGameScript {
    private       ObjAIBase _aatrox;
    private       Spell     _spell;
    private       Buff      _buff;
    private const string    AutoAttack3 = "AatroxBasicAttack3"; //Aatrox Power
    private const string    AutoAttack4 = "AatroxBasicAttack4";
    private const string    AutoAttack5 = "AatroxBasicAttack5";
    private const string    AutoAttack6 = "AatroxBasicAttack6"; //Aatrox Power
    private       Particle  _p1, _p2;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _buff                                      = buff;
        _aatrox                                       = ownerspell.CastInfo.Owner;
        _spell = ownerspell;
        _aatrox.SetAutoAttackSpell(_aatrox.HasBuff("AatroxR") ? AutoAttack6 : AutoAttack3, false);
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        if (_aatrox.HasBuff("AatroxR")) {
            _aatrox.SetAutoAttackSpells(false, AutoAttack4, AutoAttack5);
        } else {
            _aatrox.ResetAutoAttackSpell();
        }
    }
    
}