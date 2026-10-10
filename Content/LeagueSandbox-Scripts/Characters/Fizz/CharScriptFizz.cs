using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts
{
    public class CharScriptFizz : ICharScript
    {
        private Buff _passive, _display, _trident;

        public void OnActivate(ObjAIBase owner, Spell spell = null)
        {
            _passive = AddBuff("FizzPassive", 25000, 1, spell, owner, owner, infiniteduration: true);
            _display = AddBuff("FizzPassiveDisplayIcon", 25000, 1, spell, owner, owner, infiniteduration: true);
            _trident = AddBuff("FizzTridentOnHit", 25000, 1, owner.Spells[1], owner, owner, infiniteduration: true);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell = null)
        {
            RemoveBuff(_passive);
            RemoveBuff(_display);
            RemoveBuff(_trident);
            owner.RemoveBuffsWithName("FizzPlayful");
            owner.RemoveBuffsWithName("FizzLandingRecovery");
            owner.RemoveBuffsWithName("FizzTrickSlamSoundDummy");
            owner.RemoveBuffsWithName("FizzSeastonePassive");
        }
    }
}
