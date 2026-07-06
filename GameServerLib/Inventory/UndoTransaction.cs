using System;
using System.Collections.Generic;

namespace LeagueSandbox.GameServer.Inventory
{
    /// <summary>
    /// One reversible shop action (buy or sell) recorded on the per-champion undo stack so the undo
    /// button (C2S_UndoItemReq) can restore the prior gold + inventory. The client only ever sees the
    /// stack COUNT (S2C_SetUndoEnabled); all the data needed to actually reverse the action lives here,
    /// server-side, because no game-client decomp models it. See docs/SHOP_PACKETS_PLAN.md.
    /// </summary>
    public sealed class UndoTransaction
    {
        public enum ActionKind
        {
            Buy,
            Sell
        }

        /// <summary>A captured item — what it was and where it sat — enough to re-place it on undo.</summary>
        public readonly record struct ItemSnapshot(int ItemId, byte Slot, int StackCount, int SpellCharges);

        public ActionKind Kind { get; }

        /// <summary>The bought item (its resulting slot) for Buy, or the sold item for Sell.</summary>
        public ItemSnapshot Item { get; }

        /// <summary>Gold paid (Buy) or refunded (Sell); always non-negative.</summary>
        public float Gold { get; }

        /// <summary>Components consumed by a build-combine on Buy, to restore on undo. Empty otherwise.</summary>
        public IReadOnlyList<ItemSnapshot> ConsumedComponents { get; }

        private UndoTransaction(ActionKind kind, ItemSnapshot item, float gold, IReadOnlyList<ItemSnapshot> consumed)
        {
            Kind = kind;
            Item = item;
            Gold = gold;
            ConsumedComponents = consumed ?? Array.Empty<ItemSnapshot>();
        }

        public static UndoTransaction ForBuy(ItemSnapshot bought, float goldPaid,
            IReadOnlyList<ItemSnapshot> consumedComponents = null)
        {
            return new UndoTransaction(ActionKind.Buy, bought, goldPaid, consumedComponents);
        }

        public static UndoTransaction ForSell(ItemSnapshot sold, float goldRefunded)
        {
            return new UndoTransaction(ActionKind.Sell, sold, goldRefunded, Array.Empty<ItemSnapshot>());
        }
    }
}
