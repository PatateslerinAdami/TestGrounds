using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptUdyr : ICharScript {
    private ObjAIBase _udyr;
    private Spell     _spell;

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _udyr = owner;
        _spell = spell;
        for (short i = 0; i < 4; i++) {
            ApiEventManager.OnSpellCast.AddListener(this, _udyr.Spells[i], OnSpellCast);
        }
    }

    private void OnSpellCast(Spell spell) {
        AddBuff("UdyrPassiveBuff", 1f, 1, spell, _udyr, _udyr);
        AddBuff("UdyrMonkeyAgilityBuff", 5f, 1, spell, _udyr, _udyr);
    }
}