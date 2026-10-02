using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace ItemPassives
{
    public class ItemID_3078 : RageItem
    {
        protected override int ItemId => 3078;

        private readonly TrinitySpellblade _spellblade = new TrinitySpellblade();
        private class TrinitySpellblade : SpellbladeItem
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            _spellblade.OnActivate(owner);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            base.OnDeactivate(owner);
            _spellblade.OnDeactivate(owner);
        }
    }
}
