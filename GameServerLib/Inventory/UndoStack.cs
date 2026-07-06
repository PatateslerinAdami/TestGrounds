using System.Collections.Generic;

namespace LeagueSandbox.GameServer.Inventory
{
    /// <summary>
    /// Bounded LIFO stack of <see cref="UndoTransaction"/>s for one champion's shopping session.
    /// A pure data structure (no networking) so the undo bookkeeping is unit-testable in isolation;
    /// <see cref="Shop"/> owns one and emits S2C_SetUndoEnabled after each mutation. Cleared when the
    /// champion leaves the fountain (see docs/SHOP_PACKETS_PLAN.md, D1).
    /// </summary>
    public sealed class UndoStack
    {
        // The client mirrors Count in a single byte (S2C_SetUndoEnabled); keep well under 255.
        public const int MaxDepth = 64;

        private readonly List<UndoTransaction> _entries = new List<UndoTransaction>();

        public int Count => _entries.Count;
        public bool IsEmpty => _entries.Count == 0;

        /// <summary>Pushes a transaction. At MaxDepth the oldest is dropped (it can no longer be undone).</summary>
        public void Push(UndoTransaction transaction)
        {
            _entries.Add(transaction);
            if (_entries.Count > MaxDepth)
            {
                _entries.RemoveAt(0);
            }
        }

        /// <summary>Removes and returns the most recent transaction, or null if empty.</summary>
        public UndoTransaction Pop()
        {
            if (_entries.Count == 0)
            {
                return null;
            }

            var top = _entries[^1];
            _entries.RemoveAt(_entries.Count - 1);
            return top;
        }

        /// <summary>Returns the most recent transaction without removing it, or null if empty.</summary>
        public UndoTransaction Peek()
        {
            return _entries.Count == 0 ? null : _entries[^1];
        }

        /// <summary>Clears all entries. Returns true if anything was removed.</summary>
        public bool Clear()
        {
            if (_entries.Count == 0)
            {
                return false;
            }

            _entries.Clear();
            return true;
        }
    }
}
