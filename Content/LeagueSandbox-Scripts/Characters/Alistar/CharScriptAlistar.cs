
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptAlistar : ICharScript {
    private ObjAIBase _alistar;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _alistar = owner;
        for (short i = 0; i < 4; i++) {
            ApiEventManager.OnSpellPostCast.AddListener(this, _alistar.Spells[i], OnSpellPostCast);
        }
    }

    private void OnSpellPostCast(Spell spell) {
        AddBuff("TrampleBuff", 3f, 1, spell, _alistar, _alistar);
    }
}