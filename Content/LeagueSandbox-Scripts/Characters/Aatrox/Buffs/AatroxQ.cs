using System.Numerics;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

// Aatrox Q — ASCEND phase (self-buff on Aatrox). Replay 663eda09: hidden COMBAT_ENCHANCER on the
// caster, added at cast. Drives the leap-up (parabola dash, gravity 10) and, after a delay, hands off
// to the AatroxQDescent self-buff which does the dive. The enemy knockup at landing is AatroxQKnockup.
// lockActions:false — Riot does NOT seal Aatrox's spellbook during Q (CAN_CAST stays 1 the whole leap).
internal class AatroxQ : IBuffGameScript {
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1,
        IsHidden    = true
    };

    public StatsModifier StatsModifier { get; } = new();

    // Replay-measured (see Spells/Aatrox/Q.cs for the centered/halved-coord reasoning):
    // ascend = ~48.8u WORLD span at ~25.8 u/s (≈0.95s flight) with ParabolicGravity=10; the dive
    // takes over ~0.40s after the ascend starts.
    private const float AscendDistance             = 48.8f;
    private const float AscendSpeed                = 25.8f;
    // Ascend→descent transition is a CONSTANT ~358ms in the replay (52 casts, median 358ms, ±~30ms tick
    // jitter, r=-0.22 vs cast distance ⇒ distance-independent). Equivalent to the parabola apex of the
    // fixed-param ascend arc — but since it never varies, a fixed timer reproduces it exactly (no
    // server-side parabola sim / apex event needed).
    private const float JumpToDescentDelaySeconds  = 0.358f;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        var endPos = (ownerSpell.Script as Spells.AatroxQ)?.EndPos ?? unit.Position;

        unit.StopMovement(networked: false);
        FaceDirection(endPos, unit, true);
        // Riot disables ONLY auto-attack during the leap (replay 663eda09: CAN_ATTACK=0 on the ascend,
        // back to 1 at landing); CanCast/CanMove stay enabled. Re-enabled in AatroxQDescent at landing.
        unit.SetStatus(StatusFlags.CanAttack, false);

        var direction  = new Vector2(unit.Direction.X, unit.Direction.Z);
        var jumpTarget = unit.Position + direction * AscendDistance;
        ForceMove(unit, jumpTarget, AscendSpeed, gravity: 10f, facing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING, lockActions: false, movementName: "AatroxQAscend");

        // Hand off to the descent phase. The ascend force-move is ended by the descent's own dash.
        // Remove the ascend buff here (replay: AatroxQ lifetime ≈ this delay) and start the descent
        // buff (script-controlled / infiniteduration, removed at landing).
        unit.RegisterTimer(new GameScriptTimer(JumpToDescentDelaySeconds, () => {
            RemoveBuff(unit, "AatroxQ");
            if (!unit.IsDead) {
                AddBuff("AatroxQDescent", 0f, 1, ownerSpell, unit, (ObjAIBase)unit, infiniteduration: true);
            }
        }));
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}
