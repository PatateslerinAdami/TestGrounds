using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

internal class WrathoftheAncients : ICharScript {
    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        AddBuff("GlobalMonsterBuff",      25000.0f, 1, spell, owner, owner, true);
        AddBuff("CrestoftheAncientGolem", 25000.0f, 1, spell, owner, owner, true);
    }
}