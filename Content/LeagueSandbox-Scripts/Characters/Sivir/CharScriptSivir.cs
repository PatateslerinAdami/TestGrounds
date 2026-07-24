using System.Linq;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptSivir : ICharScript {
    private ObjAIBase _owner;

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
        AddBuff("SivirPassive", 25000f, 1, spell, _owner, _owner, true);
    }
}