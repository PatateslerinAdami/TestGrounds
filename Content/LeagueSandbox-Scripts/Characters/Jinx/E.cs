using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class JinxE : ISpellScript {
    private ObjAIBase _jinx;
    private Vector2 _targetPosition;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _jinx = owner; }


    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _targetPosition = end;
        FaceDirection(_targetPosition, _jinx, true);
        PlayAnimation(_jinx, "Spell3", 0.5f, flags: AnimationFlags.Override);
    }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell) {
        var direction = _targetPosition - _jinx.Position;
        if (direction.LengthSquared() < 0.0001f) {
            direction = new Vector2(_jinx.Direction.X, _jinx.Direction.Z);
        }

        var forward = Vector2.Normalize(direction);
        var perpendicular = new Vector2(-forward.Y, forward.X);

        var leftTarget = _targetPosition + perpendicular * 200.0f;
        var centerTarget = _targetPosition;
        var rightTarget = _targetPosition - perpendicular * 200.0f;
        
        SpellCast(_jinx, 4, SpellSlotType.ExtraSlots, _jinx.Position, leftTarget, true, Vector2.Zero);
        SpellCast(_jinx, 4, SpellSlotType.ExtraSlots, _jinx.Position, centerTarget, true, Vector2.Zero);
        SpellCast(_jinx, 4, SpellSlotType.ExtraSlots, _jinx.Position, rightTarget, true, Vector2.Zero);
    }
}

public class JinxEHit : ISpellScript {
    private ObjAIBase _jinx;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = false,
        CastTime = 0
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _jinx = owner; }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _spell = spell;
        CreateMissile(spell, end);
    }

    private void CreateMissile(Spell spell, Vector2 targetPosition) {
        var missile = spell.CreateSpellMissile(new MissileParameters {
            Type = MissileType.Arc,
            OverrideEndPosition = targetPosition,
        });
        if (missile == null) {
            return;
        }
        missile.SetSpeed(1500f);
        LogInfo("" + missile.GetSpeed());

        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd);
    }

    private void OnMissileEnd(SpellMissile missile) {
        var chomper = AddMinion(_jinx, "JinxMine", "FlameChompers", missile.Position, _jinx.Team, _jinx.SkinID, true, false, false, 0);
        if (chomper == null) {
            return;
        }
        AddBuff("JinxEMine", 5.75f, 1, _spell, chomper, _jinx);
        FaceDirection(GetPointFromUnit(_jinx, 400, 0), chomper, true);
    }
}
