using System.Linq;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptNami : ICharScript {
    private ObjAIBase _owner;

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _owner = owner;
    }
}