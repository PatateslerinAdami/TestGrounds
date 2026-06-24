using LeagueSandbox.GameServer.GameObjects.StatsNS;
using GameServerCore.Enums;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    public class Minion : ObjAIBase
    {
        /// <summary>
        /// Unit which spawned this minion.
        /// </summary>
        public ObjAIBase Owner { get; }
        /// <summary>
        /// Whether or not this minion should ignore collisions.
        /// </summary>
        public bool IgnoresCollision { get; protected set; }
        /// <summary>
        /// Whether or not this minion is considered a ward.
        /// </summary>
        public bool IsWard { get; protected set; }
        /// <summary>
        /// Riot CharacterState::IsMinionAcquirable — whether minions may auto-acquire this unit as a
        /// target. Default true; set false to make a unit invisible to minion target acquisition.
        /// Only consulted when the acquirer is itself a minion (see <see cref="IsTargetableByUnit"/>).
        /// </summary>
        public bool IsMinionAcquirable { get; set; } = true;

        /// <summary>
        /// Riot obj_AI_Minion::IsTargetableByUnit override: when the acquirer is a minion, a minion
        /// target is additionally gated on being minion-acquirable and not a ward (minions never
        /// auto-attack wards), on top of the base global + per-team targetability.
        /// </summary>
        public override bool IsTargetableByUnit(AttackableUnit acquirer)
        {
            if (acquirer is Minion)
            {
                if (!IsMinionAcquirable || IsWard)
                {
                    return false;
                }
            }
            return base.IsTargetableByUnit(acquirer);
        }

        // Wards don't auto-provide vision; their sight comes solely from the explicit
        // perception-bubble Region created by the ward script (which also carries reveal-stealth
        // for control/vision wards). Avoids double-providing alongside that Region.
        public override bool AutoProvidesVision => !IsWard;
        /// <summary>
        /// Whether or not this minion is a LaneMinion.
        /// </summary>
        public bool IsLaneMinion { get; protected set; }
        /// <summary>
        /// Whether or not this minion is targetable at all.
        /// </summary>
        public bool IsTargetable { get; protected set; }
        /// <summary>
        /// Extra raw bits to OR into the SpawnMinionS2C bitfield byte (bits 0x20 and 0x40
        /// observed in Riot's TestCubeRender10Vision spawn packets, purpose unconfirmed —
        /// tracked here so callers can opt in for wire-fidelity testing).
        /// </summary>
        public byte SpawnBitfieldExtra { get; set; }
        /// <summary>
        /// Only unit which is allowed to see this minion.
        /// </summary>
        public ObjAIBase VisibilityOwner { get; }

        //TODO: Implement these variables
        public int DamageBonus { get; protected set; }
        public int HealthBonus { get; protected set; }
        public int InitialLevel { get; protected set; }

        /// <summary>
        /// Roam state shared by minion-family AIs (mirrors S4 MinionRoamState
        /// {kInactive, kHostile, kRunInFear}). Gates target acquisition: a unit only aggros while
        /// <see cref="MinionRoamState.Hostile"/>, never while <see cref="MinionRoamState.Inactive"/>
        /// (dormant) or <see cref="MinionRoamState.RunInFear"/> (fleeing CC).
        /// Who drives it differs per subtype: <see cref="LaneMinion"/> is engine-managed (proximity
        /// wake via <c>UpdateRoamState</c>, AI only reads); jungle <see cref="Monster"/> is AI-driven
        /// (the Leashed AI flips it on damage/leash). <see cref="Behavior.CrowdControlComponent"/> sets
        /// RunInFear/Hostile on fear-flee for any minion.
        /// </summary>
        public MinionRoamState RoamState { get; set; }

        public Minion(
            Game game,
            ObjAIBase owner,
            Vector2 position,
            string model,
            string name,
            uint netId = 0,
            TeamId team = TeamId.TEAM_NEUTRAL,
            int skinId = 0,
            bool ignoreCollision = false,
            bool targetable = true,
            bool isWard = false,
            ObjAIBase visibilityOwner = null,
            Stats stats = null,
            string AIScript = "",
            int damageBonus = 0,
            int healthBonus = 0,
            int initialLevel = 1,
            bool enableScripts = true
        ) : base(game, model, name, 40, position, 1100, skinId, netId, team, stats, AIScript, enableScripts)
        {
            Owner = owner;

            IsLaneMinion = false;
            IsWard = isWard;
            IgnoresCollision = ignoreCollision;
            if (IgnoresCollision)
            {
                SetStatus(StatusFlags.Ghosted, true);
            }

            IsTargetable = targetable;
            if (!IsTargetable)
            {
                SetStatus(StatusFlags.Targetable, false);
            }

            VisibilityOwner = visibilityOwner;
            DamageBonus = damageBonus;
            HealthBonus = healthBonus;
            InitialLevel = initialLevel;
            MoveOrder = OrderType.Stop;

            Replication = new ReplicationMinion(this);
        }

        // NOTE (2026-06-18): the former Minion.OnCollision unit-vs-unit PUSH was REMOVED — it had
        // no Riot counterpart and was the root of minion movement over-emission. Decomp:
        // obj_AI_Minion::OnActorCollision (AIMinionClient.cpp:308) and obj_AI_Base::OnActorCollision
        // (AIBase.cpp:2391) are BOTH empty no-ops (return Continue); Riot's per-collider OnCollision
        // hook does NOT move the unit. All minion separation is Move-time in HandleActorCollision
        // (= our AttackableUnit.Move ComputeGroupCollisionResponse/ComputeAvoidanceResponse/
        // ComputeSeparationPush — the faithful S4 port). Our override DOUBLE-PUSHED minions on top of
        // Move() AND emitted via SetPosition on every per-tick collision event (PACKET_LOG snare.jsonl:
        // ~90% of minion 0x61 were re-anchors vs Riot ~16%). Minions now inherit AttackableUnit.
        // OnCollision (terrain-escape + API event publish, no unit push), matching Riot.
    }
}