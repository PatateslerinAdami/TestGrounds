using System.Numerics;
using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AlphaStrike : ISpellScript {
    private ObjAIBase _masterYi;
    private AttackableUnit _target;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = false,
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Chained,
            BounceSpellNameEnemy = "AlphaStrikeBounce",
            BounceSelection = BounceSelection.Nearest,
            MaximumHits = 5,
            CanHitSameTargetConsecutively = false,
            CanHitCaster = false,
            CanHitSameTarget = false,
            CanHitEnemies = true,
            CanHitFriends = false,
        }
        
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _masterYi = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    public void OnSpellCast(Spell spell)
    {
        AddBuff("Alpha_Strike", 4f, 1, spell, _masterYi, _masterYi);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd);
    }

    private void OnMissileEnd(SpellMissile missile)
    {
        RemoveBuff(_masterYi,"Alpha_Strike");
        SpellCast(_masterYi, 1, SpellSlotType.ExtraSlots, true, _target, _masterYi.Position);
        ApiEventManager.OnLaunchMissile.RemoveListener(this, missile.SpellOrigin, OnLaunchMissile);
        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnMissileEnd);
    }
}


public class AlphaStrikeBounce : ISpellScript {
    private ObjAIBase _alistar;
    private AttackableUnit _target;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
    {
        NotSingleTargetSpell = false,
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters
        {
        },
    };
}


public class AlphaStrikeTeleport : ISpellScript {
    private ObjAIBase      _katarina;
    private AttackableUnit _target;
    private Vector2        _previousPos;
    private Vector2        _coords;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts             = false,
        IsDamagingSpell                = true,
        CastingBreaksStealth           = false,
        NotSingleTargetSpell           = false,
        IsDeathRecapSource             = true,
        AutoFaceDirection              = false,
        // Lets OnSpellPreCast write the landing pos into CastInfo.TargetPosition without
        // Spell.Cast clobbering it with the default 10-unit-forward stub.
        OverrideTargetPositionInScript = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _katarina = owner; }
    
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        _previousPos = _katarina.Position;
      
        const float visualBuffer = 50f;
        var overshoot = _katarina.CollisionRadius + _target.CollisionRadius + visualBuffer;
        _coords = CalcVector(overshoot, _katarina.Position, _target.Position);
        // Replay-verified: CastSpellAns.targetPosition is the landing pos, not the click
        // target's center. Sets up the wire packet that Spell.Cast will broadcast next.
        var landing3D = new Vector3(_coords.X, _katarina.GetHeight(), _coords.Y);
        spell.CastInfo.TargetPosition = landing3D;
        spell.CastInfo.TargetPositionEnd = landing3D;
        
        FaceDirection(_target.Position, _katarina, true);
        var canDoSilentHandoff = IsValidTarget(_katarina, _target,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        TeleportTo(_katarina, _coords.X, _coords.Y, silent: true);
        NotifyTeleport(_katarina, _coords);
        FaceDirection(_target.Position, _katarina, true);
    }
    


    private static Vector2 CalcVector(in float distance, in Vector2 player, in Vector2 target) {
        return target - (player - target).Normalized() * (!IsWalkable(target.X, target.Y) ? -distance : distance);
    }
}