using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptAshe : ICharScript {
    private ObjAIBase _ashe;

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ashe = owner;
        AddBuff("Focus", 25000f, 1, spell, _ashe, _ashe, infiniteduration: true);
    }
}
