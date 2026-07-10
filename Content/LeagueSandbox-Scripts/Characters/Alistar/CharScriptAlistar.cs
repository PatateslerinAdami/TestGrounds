
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptAlistar : ICharScript {

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell) {
        AddBuff("ColossalStrength", 25000f, 1, spell, owner, owner, true);
    }
}