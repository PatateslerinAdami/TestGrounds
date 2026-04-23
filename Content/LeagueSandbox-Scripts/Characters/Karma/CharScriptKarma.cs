using System.Linq;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptKarma : ICharScript {
    private const byte MantraSlot = 3;
    private ObjAIBase _owner;

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _owner = owner;
        AddBuff("KarmaPassive", 25000f, 1, spell, _owner, _owner, true);
        if (_owner is not Champion champion) {
            return;
        }

        var mantraSpell = champion.GetSpell("KarmaMantra");
        if (mantraSpell == null || mantraSpell.CastInfo.SpellLevel > 0) {
            return;
        }

        champion.LevelUpSpell(MantraSlot, false);
    }
}
