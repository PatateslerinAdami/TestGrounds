using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

// The actual airborne/knockup applied to ENEMIES hit by Aatrox Q at landing (replay 663eda09:
// BuffType KNOCKUP, hash 185404368, name "AatroxQKnockup"). The two self-state buffs are AatroxQ
// (ascend) and AatroxQDescent (dive) — see those scripts.
internal class AatroxQKnockup : IBuffGameScript {
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.KNOCKUP,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        // Replay-measured (663eda09, AatroxQKnockup on hit enemies): a near-in-place vertical arc —
        // ParabolicGravity 20, speed ~10, ~10u horizontal nudge. The nudge direction VARIES per enemy
        // in the replay (not a fixed diagonal), so push each enemy away from the caster instead of a
        // fixed offset which made every target drift the same way. NO anim override — Riot sends 0
        // PlayAnimation to knocked-up enemies; the airborne pose is client-side from the parabolic
        // force-move itself (forcing "RUN" made the enemy run-animate mid-air = the weird look).
        var caster = ownerSpell.CastInfo.Owner;
        var dir = unit.Position - caster.Position;
        if (dir == Vector2.Zero) {
            dir = new Vector2(0f, 1f);
        }
        dir = Vector2.Normalize(dir);

        // Knockup = BBMove with gravity (Riot has no BBKnockup). Replay values: speed 10, gravity 20,
        // ~10u nudge away from the caster. keepFacing during the airborne arc.
        ForceMove(unit, unit.Position + dir * 10f, 10f, gravity: 20f, facing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}
