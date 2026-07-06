using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior;

namespace AIScripts
{
    // Port of Scripts/RiverCrab.lua (4.20 Rift Scuttler) on BaseAIScript. The Lua composes it from
    // AI components (WanderAlongPath + SkittishMonster + LockMovementToRiver + RiverRegions +
    // OutOfCombatRegen + Default Fear/Flee/NonAggressiveTaunt). The crab is non-aggressive: it
    // patrols its river channel back and forth, scuttles away when attacked, and regenerates out of
    // combat. Reproduced here inline (only the crab consumes these behaviours):
    //  - WanderAlongPath: 0.25s patrol to the next river waypoint, advancing within 300u, bouncing
    //    at the ends (RiverRegions IncrementWaypoint).
    //  - SkittishMonster: on being hit, flee toward the far end of the channel for ~5s
    //    (SkittishMonster.attackDuration), then resume patrolling.
    //  - OutOfCombatRegen via the shared component.
    //  - CrowdControlComponent (auto-attached) covers Fear/Flee/Taunt.
    // The patrol paths and channel borders below are byte-exact from RiverRegions.luaobj (clean unluac
    // decompile). LockMovementToRiver is ported here (ClampToRiver) — it clamps every move destination
    // into the river channel, applied to the crab's own SetStateAndMove calls (patrol + flee), which is
    // exactly what the Lua component wraps. (Map11's navgrid has NO river flag — only grass/passable/
    // vision cell flags — so the river is purely the script polylines, as here.)
    // DEFERRED:
    //  - Forced-movement (knockback/pull) is NOT routed through ClampToRiver, so a displaced crab can
    //    momentarily leave the channel (Riot's wrap is also AI-script-movement only, so this matches).
    //  - The RiverCornered reaction (ServerCastSpellOnPos slow) and the on-death scuttle buff.
    public class RiverCrabAI : BaseAIScript
    {
        // The crab is non-aggressive: it never fights, so a taunt makes it STOP rather than run to +
        // attack the taunter (Riot RiverCrab.lua registers AIComponentNonAggressiveTauntBehavior instead
        // of the default). The shared CrowdControlComponent reads this and stops the crab on taunt; its
        // wander already pauses on CC (IsCrowdControlled) and resumes when the taunt ends.
        public override bool NonAggressiveTaunt => true;

        // WanderAlongPath: advance to the next waypoint once within this distance (Lua: < 300).
        private const float WANDER_REACH = 300f;
        // SkittishMonster.attackDuration = 5 — how long the crab keeps fleeing after being hit.
        private const float SKITTISH_DURATION_MS = 5000f;
        // SkittishMonster.Timer flees to MyPos + normalize(MyPos - attackerPos) * 600.
        private const float FLEE_DISTANCE = 600f;
        // SkittishMonster.LeashedCallForHelp marks an attacker melee when within 500u (250000 sq).
        private const float SKITTISH_MELEE_RANGE_SQ = 250000f;

        // The two Map11 river channels' patrol paths (River.Path), byte-exact from a clean unluac
        // decompile of the 4.20 client Shared/Scripts/RiverRegions.luaobj. The crab picks the nearer
        // channel at spawn (RiverRegions picks by squared distance to CenterPos).
        private static readonly List<Vector2> RiverBottomLeft = new List<Vector2>
        {
            new Vector2(3720f, 9920f), new Vector2(4030f, 9460f), new Vector2(4710f, 9190f),
            new Vector2(5240f, 8950f), new Vector2(5890f, 8810f)
        };
        private static readonly List<Vector2> RiverTopRight = new List<Vector2>
        {
            new Vector2(11190f, 4660f), new Vector2(11020f, 5130f), new Vector2(10440f, 5520f),
            new Vector2(9830f, 5780f), new Vector2(9190f, 6030f)
        };
        // River.TopBorder / River.BottomBorder per channel (byte-exact from RiverRegions.luaobj). Both
        // are ordered by ascending x; LockMovementToRiver clamps a move destination's z between the two
        // borders (interpolated at the destination x) and its x to the border x-span.
        private static readonly List<Vector2> TopBorderBottomLeft = new List<Vector2>
        {
            new Vector2(2840f, 11270f), new Vector2(3100f, 11500f), new Vector2(3800f, 11000f),
            new Vector2(4500f, 9900f), new Vector2(6750f, 8650f)
        };
        private static readonly List<Vector2> BottomBorderBottomLeft = new List<Vector2>
        {
            new Vector2(2840f, 11270f), new Vector2(3600f, 9700f), new Vector2(4700f, 8900f),
            new Vector2(6115f, 8100f), new Vector2(6800f, 8600f)
        };
        private static readonly List<Vector2> TopBorderTopRight = new List<Vector2>
        {
            new Vector2(8150f, 6100f), new Vector2(8850f, 6650f), new Vector2(9500f, 6200f),
            new Vector2(10800f, 5500f), new Vector2(11300f, 4900f), new Vector2(11900f, 4000f)
        };
        private static readonly List<Vector2> BottomBorderTopRight = new List<Vector2>
        {
            new Vector2(8150f, 6100f), new Vector2(9500f, 5300f), new Vector2(10200f, 4800f),
            new Vector2(11000f, 4100f), new Vector2(11300f, 3900f), new Vector2(11900f, 4000f)
        };

        // River.Dest = 4 (1-based) / River.Dir = -1 — both channels start patrolling from Path index 3
        // (0-based) heading down. The spawn faces exactly this point (verified vs Map11 spawn FaceDir).
        private const int START_PATH_INDEX = 3;

        private Monster _crab;
        private OutOfCombatRegenComponent _regen;
        private List<Vector2> _path;
        private List<Vector2> _topBorder;
        private List<Vector2> _bottomBorder;
        private int _pathIndex;
        private int _pathDir = 1;

        // SkittishMonster.Attackers: one entry per active attacker (5s window, refreshed on hit). The
        // crab flees from the FIRST (oldest) still-active attacker. IsMelee (attacker within 500u at
        // hit time) feeds the deferred RiverCornered melee/ranged slow selection — no consumer yet.
        private sealed class SkittishAttacker
        {
            public AttackableUnit Unit;
            public float ExpiresMs;
            public bool IsMelee;
        }
        private readonly List<SkittishAttacker> _attackers = new List<SkittishAttacker>();

        protected override void OnActivateBehavior()
        {
            _crab = Owner as Monster;
            if (_crab == null)
            {
                return;
            }

            // Pick the river channel whose centre is nearer to the spawn (RiverRegions chooses between
            // the two CenterPos by squared distance).
            Vector2 pos = _crab.Position;
            float dBL = Vector2.DistanceSquared(pos, new Vector2(4200f, 9800f));
            float dTR = Vector2.DistanceSquared(pos, new Vector2(10500f, 5000f));
            bool bottomLeft = dBL < dTR;
            _path = bottomLeft ? RiverBottomLeft : RiverTopRight;
            _topBorder = bottomLeft ? TopBorderBottomLeft : TopBorderTopRight;
            _bottomBorder = bottomLeft ? BottomBorderBottomLeft : BottomBorderTopRight;
            _pathIndex = START_PATH_INDEX;
            _pathDir = -1;

            _regen = AddComponent(new OutOfCombatRegenComponent());
            // The crab never fights, so it is always "out of combat" and healing.
            _regen.Start();

            // WanderAlongPath: TimerWander at 0.25s.
            InitTimer("TimerWander", 0.25f, true, TimerWander);

            // SkittishMonster reacts to being attacked (Lua registers LeashedCallForHelp); we flee on
            // taking damage.
            ApiEventManager.OnTakeDamage.AddListener(this, _crab, OnDamaged, false);

            NetSetState(AIState.AI_MOVE);
        }

        // WanderAlongPath.Timer / SkittishMonster.Timer: flee while attackers are active, else patrol.
        private void TimerWander()
        {
            if (_crab == null || _crab.IsDead || IsCrowdControlled())
            {
                return;
            }

            // SkittishMonster: prune expired/dead attackers; while any remain the wander is paused and
            // the crab flees FLEE_DISTANCE straight away from the FIRST (oldest) attacker, recomputed
            // every tick (Lua SkittishMonster.Timer: MyPos + normalize(MyPos - attackerPos) * 600).
            float now = ApiMapFunctionManager.GameTime();
            _attackers.RemoveAll(a => a.Unit == null || a.Unit.IsDead || now >= a.ExpiresMs);
            if (_attackers.Count > 0)
            {
                AttackableUnit threat = _attackers[0].Unit;
                Vector2 away = _crab.Position - threat.Position;
                away = away.LengthSquared() > 0.0001f ? Vector2.Normalize(away) : new Vector2(0f, 1f);
                SetStateAndMove(AIState.AI_MOVE, ClampToRiver(_crab.Position + away * FLEE_DISTANCE));
                return;
            }

            // WanderAlongPath: patrol the river path, bouncing between the ends.
            SetStateAndMove(AIState.AI_MOVE, ClampToRiver(_path[_pathIndex]));
            if (Vector2.Distance(_crab.Position, _path[_pathIndex]) < WANDER_REACH)
            {
                IncrementWaypoint();
            }
        }

        // LockMovementToRiver: keep a move destination inside the river channel — clamp x to the
        // border x-span, then clamp z between the bottom and top borders (interpolated at x). Patrol
        // waypoints are already in-channel (no-op); the radial flee point is what this pulls back in,
        // so the crab scuttles along the river instead of onto land. (RiverCornered slow-cast on a
        // double-axis clamp is still deferred — needs the crab's spell.)
        private Vector2 ClampToRiver(Vector2 dest)
        {
            if (_topBorder == null || _bottomBorder == null)
            {
                return dest;
            }
            float minX = _topBorder[0].X;
            float maxX = _topBorder[_topBorder.Count - 1].X;
            if (dest.X < minX) dest.X = minX;
            else if (dest.X > maxX) dest.X = maxX;
            dest = ClampToBorder(dest, _topBorder, isTop: true);
            dest = ClampToBorder(dest, _bottomBorder, isTop: false);
            return dest;
        }

        // Lua ClampToBorder: find the border segment spanning dest.x, interpolate its z; the top border
        // caps z downward, the bottom border raises z upward — confining dest between the two edges.
        private static Vector2 ClampToBorder(Vector2 dest, List<Vector2> border, bool isTop)
        {
            for (int i = 0; i < border.Count - 1; i++)
            {
                if (border[i].X <= dest.X && dest.X <= border[i + 1].X)
                {
                    float span = border[i + 1].X - border[i].X;
                    float t = span > 0.0001f ? (dest.X - border[i].X) / span : 0f;
                    float borderZ = border[i].Y + t * (border[i + 1].Y - border[i].Y);
                    if (isTop)
                    {
                        if (borderZ < dest.Y) dest.Y = borderZ;
                    }
                    else if (dest.Y < borderZ)
                    {
                        dest.Y = borderZ;
                    }
                    break;
                }
            }
            return dest;
        }

        // RiverRegions IncrementWaypoint: advance along the path, reversing direction at either end.
        private void IncrementWaypoint()
        {
            if (_pathDir == 1 && _pathIndex == _path.Count - 1)
            {
                _pathDir = -1;
            }
            else if (_pathIndex == 0)
            {
                _pathDir = 1;
            }
            _pathIndex += _pathDir;
            _pathIndex = System.Math.Clamp(_pathIndex, 0, _path.Count - 1);
        }

        // SkittishMonster.LeashedCallForHelp: register the attacker and flee from it for attackDuration
        // (5s), refreshing the window on each hit. Riot tracks a per-attacker list and flees from the
        // first (oldest) attacker; we keep the first attacker until its window lapses, then re-arm to
        // whoever hits next.
        private void OnDamaged(DamageData damageData)
        {
            if (_crab == null || _crab.IsDead || damageData.Attacker == null || damageData.Attacker.IsDead)
            {
                return;
            }

            AttackableUnit attacker = damageData.Attacker;
            bool isMelee = Vector2.DistanceSquared(_crab.Position, attacker.Position) <= SKITTISH_MELEE_RANGE_SQ;
            float expires = ApiMapFunctionManager.GameTime() + SKITTISH_DURATION_MS;

            SkittishAttacker existing = _attackers.Find(a => a.Unit == attacker);
            if (existing != null)
            {
                existing.ExpiresMs = expires;
                existing.IsMelee = isMelee;
            }
            else
            {
                _attackers.Add(new SkittishAttacker { Unit = attacker, ExpiresMs = expires, IsMelee = isMelee });
            }
        }

        // While crowd-controlled the CrowdControlComponent owns movement; don't issue patrol orders.
        private bool IsCrowdControlled()
        {
            const StatusFlags cc = StatusFlags.Feared | StatusFlags.Charmed | StatusFlags.Taunted;
            return (_crab.Status & cc) != 0 || _crab.IsForceMoved;
        }
    }
}
