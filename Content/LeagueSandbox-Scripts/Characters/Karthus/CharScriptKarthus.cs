using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptKarthus : ICharScript {
    public void OnPostActivate(ObjAIBase owner, Spell spell) {
        // Death Defied (innate passive): a persistent, hidden buff that turns Karthus's death into a
        // 7s zombie phase (he keeps casting before truly dying). The buff persists through death
        // (PersistsThroughDeath) so it survives the death → zombie → respawn cycle and re-arms each
        // life. Long finite duration mirrors the project's "infinite" passive-buff convention.
        AddBuff("KarthusDeathDefied", 25000f, 1, spell, owner, owner, true);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) { }
}
