using System.Linq;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Inventory
{
    public class Shop
    {
        private readonly Champion _owner;
        private readonly Game _game;

        public const byte ITEM_ACTIVE_OFFSET = 6;

        private Shop(Champion owner, Game game)
        {
            _owner = owner;
            _game = game;
        }

        public bool HandleItemSellRequest(byte slotId)
        {
            var inventory = _owner.Inventory;
            var i = inventory.GetItem(slotId);
            if (i == null)
            {
                return false;
            }

            var sellPrice = i.TotalPrice * i.ItemData.SellBackModifier;
            _owner.AddGold(null, sellPrice, false);

            _owner.Inventory.RemoveItem(inventory.GetItemSlot(i), _owner);
            return true;
        }

        public bool HandleItemBuyRequest(int itemId)
        {
            var itemTemplate = _game.ItemManager.SafeGetItemType(itemId);
            if (itemTemplate == null || _owner.Stats.Level < itemTemplate.RequiredLevel
                || (!string.IsNullOrEmpty(itemTemplate.RequiredChampion) && itemTemplate.RequiredChampion != _owner.Model))
            {
                return false;
            }

            var stats = _owner.Stats;
            var inventory = _owner.Inventory;
            var price = itemTemplate.TotalPrice;
            var ownedItems = inventory.GetAvailableItems(itemTemplate.Recipe.GetItems());
            // Gold income items are limited to one; an upgrade may consume the currently owned item.
            if (itemTemplate.ItemGroup == "GoldBase"
                && inventory.GetAllItems().Except(ownedItems).Any(item => item.ItemData.ItemGroup == "GoldBase"))
                return false;
            if (ownedItems.Count != 0)
            {
                price -= ownedItems.Sum(item => item.ItemData.TotalPrice);
                if (stats.Gold < price)
                {
                    return false;
                }

                var state = new BuffVariables();
                foreach (var component in ownedItems)
                    foreach (var value in component.GetState().Values) state.Set(value.Key, value.Value);

                foreach (var items in ownedItems)
                {
                    _owner.Inventory.RemoveItem(inventory.GetItemSlot(items), _owner);
                }

                _owner.Inventory.AddItem(itemTemplate, _owner, state);
            }
            else if (stats.Gold < price || !_owner.Inventory.AddItem(itemTemplate, _owner).Value)
            {
                return false;
            }

            _owner.AddGold(null, -price, false);
            return true;
        }

        public static Shop CreateShop(Champion owner, Game game)
        {
            return new Shop(owner, game);
        }
    }
}
