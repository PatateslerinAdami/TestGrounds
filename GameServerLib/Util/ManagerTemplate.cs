using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace LeagueSandbox.GameServer.Util
{
    /// <summary>
    /// Typed object registry that mirrors Riot's <c>ManagerTemplate&lt;T, Key, Hash&gt;</c>
    /// (4.17 <c>Game/LoL/Util/LoLObjectManagerTemplate</c>).
    ///
    /// It keeps the object set in TWO parallel containers, exactly like Riot:
    ///   * <see cref="_ids"/>  — a hash map (NetId -> object) for O(1) <see cref="Find"/> and add-dedup.
    ///   * <see cref="_objects"/> — an insertion-ORDERED list that is the sole iteration surface.
    ///
    /// The ordered list is what makes iteration deterministic: Riot always iterates <c>mObjects</c>
    /// (never the hash map), and <see cref="Erase"/> is order-preserving (it shifts the tail down,
    /// NOT swap-and-pop), so external iteration order is stable across removals. This is the property
    /// a plain <see cref="Dictionary{TKey,TValue}"/> does not guarantee.
    ///
    /// Divergence from Riot, on purpose: Riot guards each container with a per-manager recursive
    /// <c>mContainerLock</c>. Note that lock only makes the paired map+vector MUTATION atomic — reads
    /// and iteration are unlocked — so it is a lightweight defensive guard (against auxiliary threads
    /// occasionally touching a manager), NOT a structure built for concurrent gameplay mutation.
    /// We need no mutex at all: every ObjectManager access is confined to the game thread. The network
    /// thread only does ENet I/O and hands packets to the game thread via thread-safe Bridge queues
    /// (PacketHandlerManager.HandleNetworkPacket enqueues; DispatchInboundRequest runs the handlers on
    /// the game thread), so there is no concurrent access to guard. Revisit if object-mutating work
    /// ever moves off the game thread.
    /// </summary>
    /// <typeparam name="T">Stored reference type (e.g. Minion, BaseTurret, Champion).</typeparam>
    public class ManagerTemplate<T> : IEnumerable<T> where T : class
    {
        // Riot's find() soft-asserts mIDs.size() < 10000 as a per-manager sanity cap; mirrored below.
        private const int SanityCap = 10000;

        // mIDs: NetId -> object, for O(1) lookup and add-time dedup.
        private readonly Dictionary<uint, T> _ids = new Dictionary<uint, T>();

        // mObjects: insertion-ordered; the ONLY thing iteration/GetArray exposes.
        private readonly List<T> _objects = new List<T>();

        /// <summary>Number of stored objects (Riot: <c>size()</c>, delegates to the ordered vector).</summary>
        public int Count => _objects.Count;

        /// <summary>
        /// Read-only view of the insertion-ordered object list (Riot: <c>GetArray()</c>).
        /// Copy-free — do not mutate the manager while iterating this.
        /// </summary>
        public IReadOnlyList<T> GetArray() => _objects;

        /// <summary>
        /// Hash lookup by NetId (Riot: <c>find(id)</c>). Returns the object, or null if the id is
        /// absent or maps to a null entry.
        /// </summary>
        public T? Find(uint id)
        {
            // Riot's soft assert: logs in debug, never clamps or fails the lookup. Debug.Assert is a
            // no-op in release builds, matching the "does not affect the return" behavior.
            Debug.Assert(_ids.Count < SanityCap, "ManagerTemplate: mIDs.Count < 10000");

            // We never store null (PushBackUnique rejects it), so a hit is always a real object.
            return _ids.TryGetValue(id, out var value) ? value : null;
        }

        /// <summary>
        /// Inserts (obj, id) into BOTH containers, unless the id is already present (Riot:
        /// <c>push_back_unique</c>). Dedup is keyed on the id only, not the object reference.
        /// </summary>
        /// <returns>true if inserted; false if the id was already present (no state change).</returns>
        public bool PushBackUnique(T obj, uint id)
        {
            // Dedup by id: a double-add is a silent no-op returning false (Riot behaviour), NOT the
            // ArgumentException that Dictionary.Add would throw.
            if (Find(id) != null)
            {
                return false;
            }

            _ids[id] = obj;
            _objects.Add(obj);
            return true;
        }

        /// <summary>
        /// Removes (obj, id) from BOTH containers (Riot: <c>erase</c>). The list removal is
        /// order-preserving (RemoveAt shifts the tail), matching Riot's <c>mObjects.erase(vecIt)</c>.
        /// </summary>
        /// <returns>
        /// The pre-erase index the object had in the ordered list, or -1 if the id was not present
        /// (mirrors Riot: it only erases when the id is found in the map).
        /// </returns>
        public int Erase(T obj, uint id)
        {
            // Both lookups happen up-front, exactly like Riot (mIDs.find + std::find over the vector).
            bool inMap = _ids.ContainsKey(id);
            int index = _objects.IndexOf(obj);

            if (inMap && index >= 0)
            {
                _ids.Remove(id);
                _objects.RemoveAt(index);
                return index;
            }

            // id missing from the map: Riot returns -1 without mutating the vector. Because
            // PushBackUnique/Erase always mutate both containers together, the containers never
            // desync, so this branch is effectively unreachable in normal use.
            Debug.Assert(index < 0, "ManagerTemplate.Erase: object present in list but id missing from map");
            return -1;
        }

        /// <summary>True if an object is registered under the given NetId.</summary>
        public bool Contains(uint id) => Find(id) != null;

        /// <summary>Empties both containers (used on teardown).</summary>
        public void Clear()
        {
            _ids.Clear();
            _objects.Clear();
        }

        // Iteration is always over the ordered list, never the hash map — matches Riot's begin()/end().
        public IEnumerator<T> GetEnumerator() => _objects.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
