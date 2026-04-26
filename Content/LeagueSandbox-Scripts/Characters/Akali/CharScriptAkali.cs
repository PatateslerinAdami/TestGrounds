using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptAkali : ICharScript {
    private ObjAIBase _akali;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _akali = owner;
        AddBuff("AkaliTwinDmg", 250000f, 1, spell, _akali, _akali, true);
        AddBuff("AkaliTwinAp", 250000f, 1, spell, _akali, _akali, true);
        AddBuff("AkaliTwinDisciplines", 250000f, 1, spell, _akali, _akali, true);
    }
}