using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class VayneTumble : ISpellScript {
    private ObjAIBase     _vayne;
    private Spell _tumbleSpell;
    private bool  _tumbleDashPending;
    private const float TumbleDistance   = 300.0f;

    private static float GetTumbleAdRatio(int spellLevel) {
        return spellLevel switch {
            5 => 0.50f,
            4 => 0.45f,
            3 => 0.40f,
            2 => 0.35f,
            _ => 0.30f
        };
    }

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _vayne = owner;
        _tumbleSpell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _vayne, OnUpdateStats);
        ApiEventManager.OnMoveSuccess.AddListener(this, _vayne, OnMoveSuccess);
        ApiEventManager.OnMoveFailure.AddListener(this, _vayne, OnMoveFailure);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var from = _vayne.Position;
        var dir  = end - from;

        // Avoid NaN issue if click is exactly on Vayne
        if (dir.LengthSquared() < 0.0001f)
            dir = new Vector2(_vayne.Direction.X, _vayne.Direction.Z);

        dir = Vector2.Normalize(dir);

        var dashEnd = from + dir * TumbleDistance;
        if (_vayne.HasBuff("VayneInquisition")) {
            AddBuff("VayneInquisitionStealth", 1f, 1, spell, _vayne, _vayne);
        }

        FaceDirection(dashEnd, _vayne, true);
        PlayAnimation(_vayne, "Spell1", 0.425f);
        _tumbleDashPending = true;
        ForceMovement(_vayne, "", dashEnd, 500 + _vayne.Stats.MoveSpeed.Total, 0.0f, 0.0f, 0.0f,
                      movementType: ForceMovementType.FIRST_WALL_HIT,
                      movementOrdersType: ForceMovementOrdersType.CANCEL_ORDER);
    }

    private void OnMoveSuccess(AttackableUnit owner, ForceMovementParameters parameters) {
        if (owner != _vayne || !_tumbleDashPending) return;
        _tumbleDashPending = false;
        AddBuff("VayneTumble", 6f, 1, _tumbleSpell, _vayne, _vayne);
    }

    private void OnMoveFailure(AttackableUnit owner, ForceMovementParameters parameters) {
        if (owner != _vayne) return;
        _tumbleDashPending = false;
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var level = _tumbleSpell?.CastInfo.SpellLevel ?? 1;
        var ratio = GetTumbleAdRatio(level);
        SetSpellToolTipVar(unit, 0, _vayne.Stats.AttackDamage.Total * ratio, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}

public class VayneTumbleAttack : ISpellScript {
    private ObjAIBase _vayne;
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _vayne = owner;
    }
}

public class VayneTumbleUltAttack : ISpellScript {
    private ObjAIBase _vayne;
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _vayne = owner;
    }
}
