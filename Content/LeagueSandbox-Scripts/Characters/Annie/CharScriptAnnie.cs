using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptAnnie : ICharScript {
    private ObjAIBase _owner;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _owner = owner;
    }
}