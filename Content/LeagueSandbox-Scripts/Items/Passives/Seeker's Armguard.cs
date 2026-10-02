using System;
using System.Linq;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives
{
    public class ItemID_3191 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            ApiEventManager.OnKillUnit.AddListener(this, owner, Kill);
            Refresh();
        }

        private void Kill(DeathData data)
        {
            foreach (var item in _owner.Inventory.GetAllItems().Where(i => i.ItemData.ItemId == 3191))
            {
                item.GetState().Set("seekerStacks", Math.Min(30, item.GetState().GetInt("seekerStacks") + 1));
            }

            Refresh();
        }

        private void Refresh()
        {
            _owner.RemoveStatModifier(StatsModifier);
            var stacks = _owner.Inventory.GetAllItems().Where(i => i.ItemData.ItemId == 3191).Sum(i => i.GetState().GetInt("seekerStacks"));
            StatsModifier.Armor.FlatBonus = stacks * 0.5f;
            StatsModifier.AbilityPower.FlatBonus = stacks * 0.5f;
            _owner.AddStatModifier(StatsModifier);
            foreach (var item in _owner.Inventory.GetAllItems().Where(i => i.ItemData.ItemId == 3191))
            {
                float amount = item.GetState().GetInt("seekerStacks") * 0.5f;
                ItemHelpers.ItemEffectHelpers.UpdateTooltip(_owner, item, 0, amount);
                ItemHelpers.ItemEffectHelpers.UpdateTooltip(_owner, item, 1, amount);
            }
        }

        public void OnUpdate(float diff)
        {
            Refresh();
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
