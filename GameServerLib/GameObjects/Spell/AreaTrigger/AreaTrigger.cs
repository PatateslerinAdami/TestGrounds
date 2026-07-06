using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;

namespace LeagueSandbox.GameServer.GameObjects.SpellNS.AreaTriggers
{
    /// <summary>
    /// Server-side geometric trigger region — Riot <c>LoL::AreaTriggerI</c> (mac-decomp
    /// <c>Game/LoL/AI/Script/AreaTrigger.{h,cpp}</c>). This is the faithful replacement for the
    /// LeagueSandbox <c>SpellSector</c> invention (see <c>docs/AREATRIGGER_REWRITE_PLAN.md</c>, audit H3).
    ///
    /// <para>It is deliberately NOT a <see cref="GameObject"/>: it has no NetId, is never replicated, and
    /// is invisible (the visual is separate particles the spell script spawns). It only tests unit/missile
    /// presence and fires callbacks; the spell script owns all gameplay logic (damage / buff / missile
    /// destruction). Riot has exactly two shapes — <see cref="AreaTriggerSphere"/> and
    /// <see cref="AreaTriggerWall"/>. Created and ticked by <see cref="AreaTriggerManager"/>.</para>
    /// </summary>
    public abstract class AreaTrigger
    {
        /// <summary>Server-internal id (Riot mAreaTriggerID — an int, NOT a Net::NET_ID).</summary>
        public int Id { get; }

        // Riot functors set at creation. null = no-op. The creating spell script supplies these as
        // closures over its own logic (P3); the engine only invokes them.
        private readonly Action<AttackableUnit> _onEnter;
        private readonly Action<AttackableUnit> _onExit;
        private readonly Action<AttackableUnit> _onUpdate;
        private readonly Action<SpellMissile> _onDestroyMissile;

        /// <summary>
        /// NetIds of units currently inside, for OnEnter/OnExit edge-diffing by
        /// <see cref="AreaTriggerManager"/>. Engine bookkeeping — not on the wire.
        /// </summary>
        internal readonly HashSet<uint> UnitsInside = new HashSet<uint>();

        protected AreaTrigger(int id, Action<AttackableUnit> onEnter, Action<AttackableUnit> onExit,
            Action<AttackableUnit> onUpdate, Action<SpellMissile> onDestroyMissile)
        {
            Id = id;
            _onEnter = onEnter;
            _onExit = onExit;
            _onUpdate = onUpdate;
            _onDestroyMissile = onDestroyMissile;
        }

        /// <summary>Riot <c>AreaTriggerI::UnitInArea</c> — is the unit geometrically inside this region.</summary>
        public abstract bool UnitInArea(AttackableUnit unit);

        /// <summary>
        /// Riot <c>AreaTriggerI::DestroysMissile</c> — default false. Only <see cref="AreaTriggerWall"/>
        /// (Windwall) overrides this. Wired into the missile path in P2.
        /// </summary>
        public virtual bool DestroysMissile(SpellMissile missile) => false;

        internal void FireEnter(AttackableUnit u) => _onEnter?.Invoke(u);
        internal void FireExit(AttackableUnit u) => _onExit?.Invoke(u);
        internal void FireUpdate(AttackableUnit u) => _onUpdate?.Invoke(u);
        internal void FireDestroyMissile(SpellMissile m) => _onDestroyMissile?.Invoke(m);

        /// <summary>Squared distance from point <paramref name="p"/> to segment [<paramref name="a"/>,<paramref name="b"/>].</summary>
        protected static float DistanceSquaredToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = ab.LengthSquared();
            if (len2 < 1e-6f)
            {
                return Vector2.DistanceSquared(p, a);
            }
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
            return Vector2.DistanceSquared(p, a + ab * t);
        }
    }

    /// <summary>
    /// Riot <c>LoL::AreaTriggerSphere</c> — a circular region (center + radius). The presence test is on
    /// the unit's center (Riot's sphere is a point-in-sphere test; refine with the unit collision radius
    /// in P3 if a concrete spell needs edge-inclusive behaviour).
    /// </summary>
    public class AreaTriggerSphere : AreaTrigger
    {
        private Vector2 _center;
        // Optional object the center tracks (Riot AreaTrigger attach-to-unit). When set, the region
        // follows the object live every scan — e.g. Fiddlesticks R Crowstorm follows the caster.
        private readonly GameObject _follow;

        public Vector2 Center
        {
            get => _follow != null ? _follow.Position : _center;
            set => _center = value;
        }
        public float Radius { get; set; }

        public AreaTriggerSphere(int id, Vector2 center, float radius, GameObject follow,
            Action<AttackableUnit> onEnter, Action<AttackableUnit> onExit,
            Action<AttackableUnit> onUpdate, Action<SpellMissile> onDestroyMissile)
            : base(id, onEnter, onExit, onUpdate, onDestroyMissile)
        {
            _center = center;
            _follow = follow;
            Radius = radius;
        }

        public override bool UnitInArea(AttackableUnit unit)
        {
            return Vector2.DistanceSquared(Center, unit.Position) <= Radius * Radius;
        }
    }

    /// <summary>
    /// Riot <c>LoL::AreaTriggerWall</c> — a thick line segment (Windwall: Yasuo W). Tests units against the
    /// segment within <see cref="Thickness"/>, and (when <see cref="DestroysMissiles"/>) destroys crossing
    /// enemy missiles. The swept missile-path test + exact team semantics are finalised against replay in P2;
    /// for now <see cref="DestroysMissile"/> uses the missile's current position.
    /// </summary>
    public class AreaTriggerWall : AreaTrigger
    {
        public Vector2 P1 { get; set; }
        public Vector2 P2 { get; set; }
        public float Thickness { get; }
        public bool DestroysMissiles { get; }
        /// <summary>Team the wall belongs to (Riot mTeamID); the wall blocks the OTHER team's missiles.</summary>
        public TeamId WallTeam { get; }

        public AreaTriggerWall(int id, Vector2 p1, Vector2 p2, float thickness, bool destroysMissiles, TeamId wallTeam,
            Action<AttackableUnit> onEnter, Action<AttackableUnit> onExit,
            Action<AttackableUnit> onUpdate, Action<SpellMissile> onDestroyMissile)
            : base(id, onEnter, onExit, onUpdate, onDestroyMissile)
        {
            P1 = p1;
            P2 = p2;
            Thickness = thickness;
            DestroysMissiles = destroysMissiles;
            WallTeam = wallTeam;
        }

        public override bool UnitInArea(AttackableUnit unit)
        {
            float half = Thickness * 0.5f;
            return DistanceSquaredToSegment(unit.Position, P1, P2) <= half * half;
        }

        public override bool DestroysMissile(SpellMissile missile)
        {
            if (!DestroysMissiles || missile == null || missile.Team == WallTeam)
            {
                return false;
            }
            // P2 TODO: swept segment (prev→cur missile position) ∩ wall, replay-exact team filter.
            float reach = Thickness * 0.5f + missile.CollisionRadius;
            return DistanceSquaredToSegment(missile.Position, P1, P2) <= reach * reach;
        }
    }
}
