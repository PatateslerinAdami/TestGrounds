using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;
//Blue Team Base Turrets
public class CharScriptOrderTurretDragon : ICharScript {
    private ObjAIBase _turret;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _turret = owner;
        AddBuff("TurretFortification", 420000f, 1, spell, _turret, _turret);
        AddBuff("ReinforcedArmor",     420000f, 1, spell, _turret, _turret, infiniteduration: true);
        AddBuff("PenetratingBullets",  420000f, 1, spell, _turret, _turret, infiniteduration: true);
    }
}
