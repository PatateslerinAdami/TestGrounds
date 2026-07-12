using System;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Logging;
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
        // Replay-attributed (172 casts): Spell3/Spell3_Run are sent with no flags.
        PlayAnimation(_jinx, _jinx.IsPathEnded() ? "Spell3" : "Spell3_Run", 0.5f, flags: AnimationFlags.None);
    }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell) {
        var direction = _targetPosition - _jinx.Position;
        if (direction.LengthSquared() < 0.0001f) {
            direction = new Vector2(_jinx.Direction.X, _jinx.Direction.Z);
        }

        var forward = Vector2.Normalize(direction);
        var perpendicular = new Vector2(-forward.Y, forward.X);

        
        var leftTarget = AddMinion(_jinx, "TestCubeRender10Vision", "k", _targetPosition + perpendicular * 200.0f, _jinx.Team, 0, true, false, isVisible: false, rooted: true, magicImmune: true, invulnerable: true);
        var centerTarget = AddMinion(_jinx, "TestCubeRender10Vision", "k", _targetPosition, _jinx.Team, 0, true, false, isVisible: false, rooted: true, magicImmune: true, invulnerable: true);
        var rightTarget = AddMinion(_jinx, "TestCubeRender10Vision", "k", _targetPosition - perpendicular * 200.0f, _jinx.Team, 0, true,  false, isVisible: false, rooted: true, magicImmune: true, invulnerable: true);
        
        SpellCast(_jinx, 4, SpellSlotType.ExtraSlots, true, leftTarget,  Vector2.Zero);
        SpellCast(_jinx, 4, SpellSlotType.ExtraSlots, true, centerTarget,  Vector2.Zero);
        SpellCast(_jinx, 4, SpellSlotType.ExtraSlots, true, rightTarget,  Vector2.Zero);
    }
}

public class JinxEHit : ISpellScript {
    private ObjAIBase _jinx;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = false,
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Target,
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _jinx = owner; }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
        //CreateMissile(spell, end);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileHit.AddListener(this, missile, OnMissileHit);
    }

    private void OnMissileHit(SpellMissile missile, AttackableUnit target)
    {
        target.Die(CreateDeathData(false, 0, target, target, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0));
        var chomper = AddMinion(_jinx, "JinxMine", "Cupcake Trap", missile.Position, _jinx.Team, _jinx.SkinID, true, false,  0);
        AddBuff("JinxEMine", 5.75f, 1, _spell, chomper, _jinx);
        // Facing: engine-wide spawn default is world +Z (GameObject.DEFAULT_FACING) — matches
        // Riot's wire (spawn 0xBA MovementDataStop Forward = (0, 1)); no explicit FaceDirection.
        ApiEventManager.OnLaunchMissile.RemoveListener(this, _spell, OnLaunchMissile);
        ApiEventManager.OnSpellMissileHit.RemoveListener(this, missile, OnMissileHit);
        missile.SetToRemove();
    }
}
