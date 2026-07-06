using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Inventory
{
    public class Shop
    {
        private readonly Champion _owner;
        private readonly Game _game;

        public const byte ITEM_ACTIVE_OFFSET = 6;

        // S2C_SetShopEnabled state: Enabled = master gate (blocks buy AND sell when false);
        // ForceEnabled = bypass the near-a-shop location test (set while dead / recalling).
        public bool Enabled { get; private set; } = true;
        public bool ForceEnabled { get; private set; }

        // Per-champion undo bookkeeping (docs/SHOP_PACKETS_PLAN.md). The client mirrors only the
        // count via S2C_SetUndoEnabled; the actual reversal data lives in the transactions.
        private readonly UndoStack _undoStack = new UndoStack();
        public int UndoStackSize => _undoStack.Count;

        // Active shop item substitutions (originalItemId -> substituteItemId): the shop shows/sells the
        // substitute wherever the original would appear. Driven by scripts via
        // ApiFunctionManager.SetShopItemSubstitution (e.g. Culinary Master mastery swaps 2003 -> 2010).
        private readonly Dictionary<int, int> _itemSubstitutions = new Dictionary<int, int>();

        // Server-side "near a shop" radius around the team fountain. The CLIENT runs its own
        // InRangeToShop check (it greys the buy/sell buttons by the real shop-object position +
        // UI_HW_MAXSHOPDISTANCE, which isn't a literal in the decomp), so this is deliberately GENEROUS:
        // it must never reject a buy the client allowed, while still excluding a champion that has left
        // the base (so the undo stack clears). We approximate the shop with the fountain position
        // (docs/SHOP_PACKETS_PLAN.md D3 — we don't binary-parse room.dsc/.sco). Tune if it false-rejects.
        private const float ShopRangeUnits = 1500f;
        private Vector2? _fountainPosition;

        private Shop(Champion owner, Game game)
        {
            _owner = owner;
            _game = game;
        }

        /// <summary>
        /// Whether the owner may buy/sell right now: master-enabled AND (force-enabled OR within
        /// range of a friendly shop). Mirrors the client's CanShop gate.
        /// </summary>
        public bool CanShop()
        {
            return Enabled && (ForceEnabled || IsNearShop());
        }

        private bool IsNearShop()
        {
            // Fountain position is fixed per team; cache it on first use (MapScript is ready by gameplay).
            _fountainPosition ??= _game.Map.MapScript.GetFountainPosition(_owner.Team);
            // Flat 2D (X/Z) distance: Position carries no height (that's GameObject.GetHeight via the nav
            // grid), so a fountain elevation offset can't skew this circle. The client uses a 3D distance
            // (ShopManager.cpp: dx²+dy²+dz²), where the height term only TIGHTENS its horizontal reach —
            // our 2D check is therefore strictly more permissive on elevation, never the other way.
            return Vector2.DistanceSquared(_owner.Position, _fountainPosition.Value) <= ShopRangeUnits * ShopRangeUnits;
        }

        /// <summary>
        /// Per-tick shop upkeep. Clears the undo stack the moment the champion can no longer shop —
        /// i.e. alive and outside the fountain range (D1). While dead, ForceEnabled keeps CanShop true,
        /// so the stack persists for fountain shopping. The UndoStackSize guard keeps this near-free when
        /// there is nothing to clear (no distance check on the common path).
        /// </summary>
        public void OnUpdate(float diff)
        {
            if (UndoStackSize > 0 && !CanShop())
            {
                ClearUndoStack();
            }
        }

        /// <summary>
        /// Registers a shop item substitution and notifies the client (S2C_ShopItemSubstitutionSet): the
        /// shop shows/sells <paramref name="substitutionItemId"/> wherever <paramref name="originalItemId"/>
        /// would appear. No-op for zero ids (mirrors the client handler). Used by scripts via
        /// ApiFunctionManager.SetShopItemSubstitution.
        /// </summary>
        public void SetItemSubstitution(int originalItemId, int substitutionItemId)
        {
            if (originalItemId == 0 || substitutionItemId == 0)
            {
                return;
            }

            _itemSubstitutions[originalItemId] = substitutionItemId;
            _game.PacketNotifier.NotifySetShopItemSubstitution(_owner, originalItemId, substitutionItemId);
        }

        /// <summary>Returns the substitute item id for <paramref name="originalItemId"/>, or the id itself if none.</summary>
        public int GetItemSubstitution(int originalItemId)
        {
            return _itemSubstitutions.TryGetValue(originalItemId, out var substitute) ? substitute : originalItemId;
        }

        /// <summary>
        /// Removes an active item substitution and notifies the client (S2C_ShopItemSubstitutionClear).
        /// NOTE: Riot never sends Clear in 4.20 (substitutions just persist) — provided for completeness.
        /// </summary>
        public void ClearItemSubstitution(int originalItemId)
        {
            _itemSubstitutions.Remove(originalItemId);
            _game.PacketNotifier.NotifySetShopItemSubstitutionClear(_owner, originalItemId);
        }

        /// <summary>Updates shop availability and notifies the owning client (S2C_SetShopEnabled).</summary>
        public void SetShopState(bool enabled, bool forceEnabled)
        {
            Enabled = enabled;
            ForceEnabled = forceEnabled;
            _game.PacketNotifier.NotifySetShopEnabled(_owner);
        }

        /// <summary>Pushes an undo record and notifies the new stack size (S2C_SetUndoEnabled).</summary>
        private void PushUndo(UndoTransaction transaction)
        {
            _undoStack.Push(transaction);
            _game.PacketNotifier.NotifySetUndoEnabled(_owner, _undoStack.Count);
        }

        /// <summary>
        /// Clears the undo stack (e.g. when the champion leaves the fountain, see D1) and notifies the
        /// client so the undo button greys out. The leave-fountain trigger lands with the D3 geometry.
        /// </summary>
        public void ClearUndoStack()
        {
            if (_undoStack.Clear())
            {
                _game.PacketNotifier.NotifySetUndoEnabled(_owner, _undoStack.Count);
            }
        }

        /// <summary>
        /// Reverses the most recent buy/sell (C2S_UndoItemReq). Restores gold + inventory, then re-syncs
        /// the whole inventory once (S2C_SetInventory_MapView) and the new undo-stack size. Returns false
        /// if the shop is unavailable or there is nothing to undo. See docs/SHOP_PACKETS_PLAN.md (P2).
        /// </summary>
        public bool HandleUndo()
        {
            if (!CanShop())
            {
                return false;
            }

            var tx = _undoStack.Pop();
            if (tx == null)
            {
                return false;
            }

            var inventory = _owner.Inventory;
            if (tx.Kind == UndoTransaction.ActionKind.Buy)
            {
                // Remove the just-bought item (decrement a stack or clear the slot), guarded by an id
                // match so a slot mutated since the buy isn't corrupted.
                var bought = inventory.GetItem(tx.Item.Slot);
                if (bought != null && bought.ItemData.ItemId == tx.Item.ItemId)
                {
                    inventory.RemoveItemSilent(tx.Item.Slot, _owner, 1);
                }

                // Refund the gold paid (propagates via stats replication, like the buy deduction did).
                _owner.AddGold(null, tx.Gold, false);

                // Restore each component consumed by a build-combine to its original slot.
                foreach (var comp in tx.ConsumedComponents)
                {
                    RestoreItem(comp);
                }
            }
            else
            {
                RestoreItem(tx.Item);
                // Take back the refund that was granted on sell.
                _owner.AddGold(null, -tx.Gold, false);
            }

            _game.PacketNotifier.NotifySetInventory(_owner);
            _game.PacketNotifier.NotifySetUndoEnabled(_owner, _undoStack.Count);
            return true;
        }

        /// <summary>Re-creates a snapshotted item in its original slot, preserving stacks and charges.</summary>
        private void RestoreItem(UndoTransaction.ItemSnapshot snapshot)
        {
            var template = _game.ItemManager.SafeGetItemType(snapshot.ItemId);
            if (template == null)
            {
                return;
            }

            var restored = _owner.Inventory.AddItemToSlotSilent(template, _owner, snapshot.Slot);
            if (restored == null)
            {
                return;
            }

            if (snapshot.StackCount > 1)
            {
                restored.SetStacks(snapshot.StackCount);
            }

            if (snapshot.SpellCharges > 0)
            {
                restored.SetSpellCharges(snapshot.SpellCharges);
            }
        }

        public bool HandleItemSellRequest(byte slotId)
        {
            if (!CanShop())
            {
                return false;
            }

            var inventory = _owner.Inventory;
            var i = inventory.GetItem(slotId);
            if (i == null)
            {
                return false;
            }

            var sellPrice = i.TotalPrice * i.ItemData.SellBackModifier;
            _owner.AddGold(null, sellPrice, false);

            // Record before removal so undo can re-place the exact item in its slot.
            PushUndo(UndoTransaction.ForSell(
                new UndoTransaction.ItemSnapshot(i.ItemData.ItemId, slotId, i.StackCount, i.SpellCharges),
                sellPrice));

            inventory.RemoveItem(inventory.GetItemSlot(i), _owner);
            return true;
        }

        public bool HandleItemBuyRequest(int itemId)
        {
            if (!CanShop())
            {
                return false;
            }

            var itemTemplate = _game.ItemManager.SafeGetItemType(itemId);
            if (itemTemplate == null)
            {
                return false;
            }

            // Reject items this map marks unpurchasable (Items.inibin "UnpurchasableItemList"). The client
            // already greys these out (mbIsPurchasable=false, ItemShopFoundry buy gate), so this only
            // hardens against a client that sends the buy anyway. Maps with no Items.json leave the set
            // empty => no gating. Mode-specific lists are deferred until a mutator system exists.
            if (_game.Map.MapData.UnpurchasableItems.Contains(itemId))
            {
                return false;
            }

            var stats = _owner.Stats;
            var inventory = _owner.Inventory;
            var price = itemTemplate.TotalPrice;
            var ownedItems = inventory.GetAvailableItems(itemTemplate.Recipe.GetItems());
            if (ownedItems.Count != 0)
            {
                price -= ownedItems.Sum(item => item.ItemData.TotalPrice);
                if (stats.Gold < price)
                {
                    return false;
                }

                // Snapshot the components (id, slot, stacks) BEFORE removal so undo can restore them.
                var consumed = ownedItems
                    .Select(it => new UndoTransaction.ItemSnapshot(
                        it.ItemData.ItemId, inventory.GetItemSlot(it), it.StackCount, it.SpellCharges))
                    .ToList();

                foreach (var items in ownedItems)
                {
                    inventory.RemoveItem(inventory.GetItemSlot(items), _owner);
                }

                var combined = inventory.AddItem(itemTemplate, _owner).Key;
                _owner.AddGold(null, -price, false);

                if (combined != null)
                {
                    PushUndo(UndoTransaction.ForBuy(
                        new UndoTransaction.ItemSnapshot(itemTemplate.ItemId, inventory.GetItemSlot(combined),
                            combined.StackCount, combined.SpellCharges),
                        price, consumed));
                }

                return true;
            }

            if (stats.Gold < price)
            {
                return false;
            }

            var bought = inventory.AddItem(itemTemplate, _owner);
            if (!bought.Value)
            {
                return false;
            }

            _owner.AddGold(null, -price, false);
            PushUndo(UndoTransaction.ForBuy(
                new UndoTransaction.ItemSnapshot(itemTemplate.ItemId, inventory.GetItemSlot(bought.Key),
                    bought.Key.StackCount, bought.Key.SpellCharges),
                price));
            return true;
        }

        public static Shop CreateShop(Champion owner, Game game)
        {
            return new Shop(owner, game);
        }
    }
}
