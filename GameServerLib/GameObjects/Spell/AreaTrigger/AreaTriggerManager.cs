using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Logging;
using log4net;

namespace LeagueSandbox.GameServer.GameObjects.SpellNS.AreaTriggers
{
    /// <summary>
    /// Server-side registry + per-tick driver for <see cref="AreaTrigger"/> regions — Riot
    /// <c>LoL::AreaTriggerManager</c> (a global singleton there; one instance per <see cref="Game"/> here).
    /// Owns the live triggers, runs the per-tick unit scan (OnEnter/OnExit edge-diff + OnUpdate), and is
    /// queried by the missile path for Windwall destruction (P2).
    ///
    /// <para>NOT part of <see cref="ObjectManager"/>: triggers are server-internal geometry, never
    /// replicated. See <c>docs/AREATRIGGER_REWRITE_PLAN.md</c> (audit H3).</para>
    /// </summary>
    public class AreaTriggerManager
    {
        private static readonly ILog _logger = LoggerProvider.GetLogger();

        private readonly Game _game;
        private readonly Dictionary<int, AreaTrigger> _triggers = new Dictionary<int, AreaTrigger>();
        private int _nextId = 1;

        public AreaTriggerManager(Game game)
        {
            _game = game;
        }

        /// <summary>Riot <c>AreaTriggerManager::GenerateAreaTriggerID</c>.</summary>
        private int GenerateId() => _nextId++;

        public int CreateSphere(Vector2 center, float radius,
            Action<AttackableUnit> onEnter = null, Action<AttackableUnit> onExit = null,
            Action<AttackableUnit> onUpdate = null, Action<SpellMissile> onDestroyMissile = null)
        {
            int id = GenerateId();
            _triggers[id] = new AreaTriggerSphere(id, center, radius, null, onEnter, onExit, onUpdate, onDestroyMissile);
            return id;
        }

        /// <summary>
        /// Like <see cref="CreateSphere"/> but the center tracks <paramref name="follow"/> every tick (Riot
        /// AreaTrigger attach-to-unit) — for owner-following zones (Fiddlesticks R Crowstorm). Lifetime is
        /// the caller's responsibility (Delete on a timer / when the source ends).
        /// </summary>
        public int CreateSphereAttached(GameObject follow, float radius,
            Action<AttackableUnit> onEnter = null, Action<AttackableUnit> onExit = null,
            Action<AttackableUnit> onUpdate = null, Action<SpellMissile> onDestroyMissile = null)
        {
            int id = GenerateId();
            var center = follow != null ? follow.Position : default;
            _triggers[id] = new AreaTriggerSphere(id, center, radius, follow, onEnter, onExit, onUpdate, onDestroyMissile);
            return id;
        }

        public int CreateWall(Vector2 p1, Vector2 p2, float thickness, bool destroysMissiles, TeamId wallTeam,
            Action<AttackableUnit> onEnter = null, Action<AttackableUnit> onExit = null,
            Action<AttackableUnit> onUpdate = null, Action<SpellMissile> onDestroyMissile = null)
        {
            int id = GenerateId();
            _triggers[id] = new AreaTriggerWall(id, p1, p2, thickness, destroysMissiles, wallTeam,
                onEnter, onExit, onUpdate, onDestroyMissile);
            return id;
        }

        /// <summary>Riot <c>AreaTriggerManager::Find</c>. Null if the id is unknown.</summary>
        public AreaTrigger Find(int id) => _triggers.TryGetValue(id, out var t) ? t : null;

        /// <summary>Riot <c>AreaTriggerManager::Delete</c>. Silently no-ops on an unknown id.</summary>
        public void Delete(int id) => _triggers.Remove(id);

        /// <summary>
        /// Per-tick driver (Riot <c>UnitScan</c> + <c>UpdateTriggers</c>): for every trigger, diff the set of
        /// units now inside against last tick (→ OnEnter/OnExit) and fire OnUpdate for those inside.
        /// Dormant fast-path while no trigger exists (the case throughout P1).
        /// </summary>
        public void Update(float diff)
        {
            if (_triggers.Count == 0)
            {
                return;
            }

            // Snapshot living attackable units once per tick. Team filtering is intentionally NOT done here
            // (Riot fires for all units; the script callback decides team) — see plan.
            var units = new List<AttackableUnit>();
            foreach (var obj in _game.ObjectManager.GetAllObjects())
            {
                if (obj is AttackableUnit u && !u.IsDead)
                {
                    units.Add(u);
                }
            }

            // ToArray: a callback may Delete a trigger or create another mid-scan.
            foreach (var trigger in _triggers.Values.ToArray())
            {
                ScanTrigger(trigger, units);
            }
        }

        private void ScanTrigger(AreaTrigger trigger, List<AttackableUnit> units)
        {
            var nowInside = new HashSet<uint>();

            // Enter + Update for units currently inside.
            foreach (var u in units)
            {
                if (!trigger.UnitInArea(u))
                {
                    continue;
                }
                nowInside.Add(u.NetId);
                if (!trigger.UnitsInside.Contains(u.NetId))
                {
                    Invoke(() => trigger.FireEnter(u));
                }
                Invoke(() => trigger.FireUpdate(u));
            }

            // Exit for units that were inside last tick but no longer are (incl. units that died/left —
            // resolve via ObjectManager; skip the callback if the object is gone but still clear the set).
            foreach (var netId in trigger.UnitsInside)
            {
                if (nowInside.Contains(netId))
                {
                    continue;
                }
                if (_game.ObjectManager.GetObjectById(netId) is AttackableUnit u)
                {
                    Invoke(() => trigger.FireExit(u));
                }
            }

            trigger.UnitsInside.Clear();
            foreach (var netId in nowInside)
            {
                trigger.UnitsInside.Add(netId);
            }
        }

        /// <summary>
        /// Riot missile-path query: does any wall destroy this missile this tick? Fires OnDestroyMissile on
        /// the first matching wall and returns true. Wired into the missile update in P2 (dormant until then).
        /// </summary>
        public bool TryDestroyMissile(SpellMissile missile)
        {
            if (_triggers.Count == 0 || missile == null)
            {
                return false;
            }
            foreach (var trigger in _triggers.Values.ToArray())
            {
                if (trigger.DestroysMissile(missile))
                {
                    Invoke(() => trigger.FireDestroyMissile(missile));
                    return true;
                }
            }
            return false;
        }

        // Callbacks run script code (P3); isolate a throwing script so it can't break the scan loop —
        // mirrors the try/catch wrapping around script invocations in Spell.cs.
        private static void Invoke(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                _logger.Error(null, e);
            }
        }
    }
}
