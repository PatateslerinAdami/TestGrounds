using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

internal class CharScriptLizardElder : ICharScript {
    public void OnActivate(ObjAIBase owner, Spell spell) {
        AddBuff("BlessingoftheLizardElder", 25000.0f, 1, owner.AutoAttackSpell,  owner, owner, true);
    }

    public void OnHitUnit(DamageData data) {
        // TODO: Multiply damage in data (currently unsupported).
    }
}