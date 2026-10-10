using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts
{
    public class CharScriptShaco : ICharScript
    {
        public void OnPostActivate(ObjAIBase owner, Spell spell = null)
        {
            if (owner is Champion && owner.Spells[3].SpellName == "HallucinateFull")
            {
                SetSpell(owner, "Hallucinate", GameServerCore.Enums.SpellSlotType.SpellSlots, 3);
            }

            AddBuff("Backstab", 0, 1, owner.Spells[0], owner, owner, true);
        }
    }
}
