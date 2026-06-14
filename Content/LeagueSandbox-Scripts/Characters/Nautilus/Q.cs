using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

// Dredge Line. Wire flow replay-verified (34a3cc3c, 18 casts):
//   - Q ANS + missile ANS same tick (cast start), missile FCM at windup end (0.25s),
//     PlayAnimation Spell1_idle alongside the FCM.
//   - Unit hit:  LineMissileHitList + DestroyClientMissile, both units pulled together
//     (WaypointGroupWithSpeed), Spell1_dash on Nautilus, NautilusAnchorDragRoot (~1s)
//     on the target.
//   - Wall hit:  DestroyClientMissile WITHOUT a hit list, Nautilus alone is pulled to
//     the wall (Spell1_dash + NPC_InstantStop_Attack), half the cooldown refunded
//     (Q Effect2 = 10.5/9.75/9/8.25/7.5).
//   - Max-range miss (~1030u): DestroyClientMissile only — the missile cast targets the
//     aim point projected to its 25000u CastRange ("fly forever"), so even the natural
//     end is a server-side kill and gets a destroy packet.
public class NautilusAnchorDrag : ISpellScript
{
    private ObjAIBase _owner;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
    }

    public void OnSpellPostCast(Spell spell)
    {
        // Anchor-extended pose while the hook flies (Riot sends this with the missile spawn).
        PlayAnimation(_owner, "Spell1_idle");

        var start = _owner.Position;
        var aim = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);
        var dir = aim - start;
        if (dir.LengthSquared() <= float.Epsilon)
        {
            dir = new Vector2(_owner.Direction.X, _owner.Direction.Z);
        }
        if (dir.LengthSquared() <= float.Epsilon)
        {
            dir = new Vector2(1.0f, 0.0f);
        }
        dir = Vector2.Normalize(dir);

        var end = start + dir * 25000f;
        SpellCast(_owner, 0, SpellSlotType.ExtraSlots, end, end, true, Vector2.Zero,
                  overrideForceLevel: spell.CastInfo.SpellLevel);
    }
}

public class NautilusAnchorDragMissile : ISpellScript
{
    private ObjAIBase _owner;

    // Replay: the two max-range misses died 1020u/1040u from launch (±1 tick at 2000u/s)
    // — Q display range 950 + overshoot.
    private const float MaxRange = 1030f;
    // Replay: wall pulls ran at 1675/1755 wire speed (2 samples). The unit-hit
    // distance-based formula (4*d+120) does NOT fit them; a constant is the best model.
    private const float WallPullSpeed = 1700f;
    // How far back along the flight direction we search for a walkable landing spot.
    private const float WallBacktrackStep = 20f;
    private const float WallBacktrackMax = 300f;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Arc
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        // CollisionHandler fires OnCollision(null, isTerrain: true) -> OnCollisionTerrain
        // whenever the missile position is unwalkable; Riot's server Lua did the same
        // check every 50u of flight (LuaOnMissileUpdateDistanceInterval = 50.01).
        ApiEventManager.OnCollisionTerrain.AddListener(this, missile, OnTerrainHit, true);
        ApiEventManager.OnSpellMissileUpdate.AddListener(this, missile, OnMissileUpdate);
    }

    private void RemoveMissileListeners(SpellMissile missile)
    {
        ApiEventManager.OnCollisionTerrain.RemoveListener(this, missile);
        ApiEventManager.OnSpellMissileUpdate.RemoveListener(this, missile, OnMissileUpdate);
    }

    private void OnMissileUpdate(SpellMissile missile, float diff)
    {
        var launch = new Vector2(missile.CastInfo.SpellCastLaunchPosition.X,
                                 missile.CastInfo.SpellCastLaunchPosition.Z);
        if (Vector2.DistanceSquared(launch, missile.Position) >= MaxRange * MaxRange)
        {
            // Natural max-range end — server-side kill, destroy packet flows (replay-faithful).
            RemoveMissileListeners(missile);
            missile.SetToRemove();
        }
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        var q = _owner.GetSpell("NautilusAnchorDrag");
        var damage = q.SpellData.EffectLevelAmount[1][q.CastInfo.SpellLevel]
                   + _owner.Stats.AbilityPower.Total * q.SpellData.Coefficient;
        target.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_MAGICAL,
                          DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);

        AddBuff("NautilusAnchorDragRoot", 1.0f, 1, spell, target, _owner);

        // Both units are dragged toward each other and meet in the middle, kept apart
        // by their combined collision radii. Replay (13 unit hits): Nautilus' pull speed
        // fits speed = dashDistance/0.25s + ~120 — i.e. a fixed 0.25s travel time plus a
        // small base. Same speed on both sides so they arrive together.
        var delta = target.Position - _owner.Position;
        var dist = delta.Length();
        var dir = dist > 1e-3f ? delta / dist : new Vector2(1.0f, 0.0f);
        var gap = _owner.CollisionRadius + target.CollisionRadius;
        var travel = MathF.Max(0f, (dist - gap) / 2f);
        var speed = travel * 4f + 120f;

        if (travel > 0f)
        {
            Dash(_owner, _owner.Position + dir * travel, speed, animation: "Spell1_dash");
            Dash(target, target.Position - dir * travel, speed, animation: "RUN");
        }

        RemoveMissileListeners(missile);
        missile.SetToRemove();
    }

    private void OnTerrainHit(GameObject obj)
    {
        if (obj is not SpellMissile missile || missile.IsToRemove())
        {
            return;
        }

        RemoveMissileListeners(missile);
        missile.SetToRemove();

        // Land on the near side of the wall: walk back along the flight direction until
        // Nautilus fits (same stepping pattern as Sion E's push clamp).
        var dir = new Vector2(missile.Direction.X, missile.Direction.Z);
        if (dir.LengthSquared() <= float.Epsilon)
        {
            dir = missile.Position - _owner.Position;
        }
        dir = Vector2.Normalize(dir);

        var endPos = missile.Position;
        for (float back = 0f; back <= WallBacktrackMax; back += WallBacktrackStep)
        {
            var p = missile.Position - dir * back;
            if (IsWalkable(p.X, p.Y, _owner.PathfindingRadius))
            {
                endPos = p;
                break;
            }
        }

        // Wall hit refunds half the cooldown (Q Effect2).
        var q = _owner.GetSpell("NautilusAnchorDrag");
        q.LowerCooldown(q.SpellData.EffectLevelAmount[2][q.CastInfo.SpellLevel]);

        Dash(_owner, endPos, WallPullSpeed, animation: "Spell1_dash");
    }
}
