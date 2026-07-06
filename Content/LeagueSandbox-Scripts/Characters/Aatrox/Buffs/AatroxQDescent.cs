using System;
using System.Linq;
using System.Numerics;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

// Aatrox Q — DESCENT/DIVE phase (self-buff on Aatrox). Replay 663eda09: hidden COMBAT_ENCHANCER on the
// caster, added ~0.40s after the ascend (AatroxQ). Drives the flat dive to the target and applies the
// landing effects (AoE damage + the AatroxQKnockup airborne on enemies).
internal class AatroxQDescent : IBuffGameScript {
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1,
        IsHidden    = true
    };

    public StatsModifier StatsModifier { get; } = new();

    private const float MaxDashRange         = 650f;
    // Dive = world flight ~0.166s (replay), speed scales with distance, capped at the observed
    // short-regime max ~3214 so long dives don't go absurdly fast. See Spells/Aatrox/Q.cs notes.
    private const float LandingDashDurationS = 0.166f;
    private const float MaxDiveSpeed         = 3214f;

    private Spell _spell;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _spell = ownerSpell;
        var script = ownerSpell.Script as Spells.AatroxQ;
        var castStart = script?.CastStart ?? unit.Position;
        var endPos    = script?.EndPos ?? unit.Position;

        FaceDirection(endPos, unit, true);

        var desiredDirection = endPos - castStart;
        var desiredDistance  = desiredDirection.Length();
        var dashTarget = desiredDistance > MaxDashRange
            ? castStart + Vector2.Normalize(desiredDirection) * MaxDashRange
            : endPos;

        var distance = (dashTarget - unit.Position).Length();
        if (distance <= 1f) {
            // No dive (already at target) → no landing event will fire; release the ascend's CanAttack
            // hold and end this phase buff here.
            unit.SetStatus(StatusFlags.CanAttack, true);
            RemoveBuff(unit, "AatroxQDescent");
            return;
        }

        var speed = Math.Min(distance / LandingDashDurationS, MaxDiveSpeed);
        // lockActions:false — keep CAN_CAST/CAN_MOVE enabled (Riot: castable through the whole Q).
        ForceMove(unit, endPos, speed, 0f, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, false, true, ForceMovementOrdersType.CANCEL_ORDER, "AatroxQDash");
        ApiEventManager.OnMoveSuccess.AddListener(this, unit, OnDiveLanded, true);
    }

    private void OnDiveLanded(AttackableUnit unit, ForceMovementParameters parameters) {
        if (parameters.MovementName != "AatroxQDash") return;
        var aatrox = (ObjAIBase)unit;
        // Landing: release the ascend's CanAttack hold (Riot: CAN_ATTACK back to 1 on landing).
        unit.SetStatus(StatusFlags.CanAttack, true);

        StopAnimation(unit, "Spell1", StopAnimationFlags.FadeOut | StopAnimationFlags.IgnoreLock);
        StopAnimation(unit, "Spell1_CLose", StopAnimationFlags.FadeOut | StopAnimationFlags.IgnoreLock);

        AddParticle(unit, null, "Aatrox_Base_Q_Land", unit.Position);

        var enemiesInKnockUpRange = GetUnitsInRange(unit, unit.Position, 150f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var enemy in enemiesInKnockUpRange.Where(u => _spell.SpellData.IsValidTarget(aatrox, u))) {
            AddBuff("AatroxQKnockup", 1f, 1, _spell, enemy, aatrox);
        }

        var enemiesInRange = GetUnitsInRange(unit, unit.Position, 300f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var enemy in enemiesInRange) {
            _spell.ApplyEffects(enemy);
        }

        // End of the descent phase — remove the (script-controlled) self-buff (Riot: AatroxQDescent
        // removed at landing).
        RemoveBuff(unit, "AatroxQDescent");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}
