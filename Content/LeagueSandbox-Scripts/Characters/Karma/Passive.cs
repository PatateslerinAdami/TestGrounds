using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace CharScripts
{

    public class CharScriptKarma : ICharScript

    {
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            int slot = ConvertAPISlot(SpellSlotType.SpellSlots, 3);
            var s = owner.GetSpell("KarmaMantra");
            s.SetLevel(1);
            owner.Stats.SetSpellEnabled((byte)slot, true);
        }
    }
}
