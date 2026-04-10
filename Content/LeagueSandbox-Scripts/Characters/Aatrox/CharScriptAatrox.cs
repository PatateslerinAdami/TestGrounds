using System.Linq;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptAatrox : ICharScript {
    public StatsModifier StatsModifier { get; } = new();

    public void OnPostActivate(ObjAIBase owner, Spell spell) {
        if (owner == null || spell == null) return;
        if (!owner.VisibleForPlayers.Any()) return;

        if (!owner.HasBuff("AatroxPassive"))
            AddBuff("AatroxPassive", 25000f, 1, spell, owner, owner, true);
        if (!owner.HasBuff("AatroxPassiveReady"))
            AddBuff("AatroxPassiveReady", 25000f, 1, spell, owner, owner, true);
    }
}
