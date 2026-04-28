using System.Collections.Generic;
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
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class VayneCondemn : ISpellScript {
    private ObjAIBase _vayne;
    private AttackableUnit _target;
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true,
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _vayne = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        AddBuff("VayneCondemnAALock", 2f, 1, spell, _vayne, _vayne);
    }

    public void OnSpellPostCast(Spell spell) {
        SpellCast(_vayne, 1, SpellSlotType.ExtraSlots, true, _target, Vector2.Zero);
        RemoveBuff(_vayne, "VayneCondemnAALock");
    }
}

public class VayneCondemnMissile : ISpellScript {
    private const float CondemnPushDistance     = 475.0f;
    private const float CondemnPushSpeed        = 2000.0f;
    private const float CondemnWallStunDuration = 1.5f;

    private ObjAIBase _vayne;
    private Spell     _spell;
    private readonly HashSet<uint> _pendingWallHits = new();

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _vayne = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        _pendingWallHits.Clear();
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target.IsDead) {
            return;
        }

        if (_vayne.GetSpell("VayneSilveredBolts").CastInfo.SpellLevel > 0) {
            var currentStacks = target.GetBuffWithName("VayneSilverDebuff")?.StackCount ?? 0;
            if (currentStacks >= 2) {
                RemoveBuff(target, "VayneSilverDebuff");
                AddParticleTarget(_vayne, target, "vayne_W_tar", target);
                AddBuff("VayneSilveredDebuff", 0.25f, 1, _vayne.GetSpell("VayneSilveredBolts"), target, _vayne);
                // STACKS_AND_RENEWS so we have to clear all stacks after the proc.
            } else { 
                AddBuff("VayneSilverDebuff", 3.5f, 1, spell, target, _vayne);
            }   
        }
        AddParticleTarget(_vayne, target, "vayne_E_tar", target);
        target.TakeDamage(_vayne, 45 + _vayne.Stats.AttackDamage.FlatBonus * 0.5f, DamageType.DAMAGE_TYPE_PHYSICAL,
                          DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);

        var castPosition = _vayne.Position;
        var pushEnd      = GetCondemnTargetPoint(target, castPosition);

        CancelDash(target);
        ApiEventManager.OnCollisionTerrain.AddListener(this, target, OnCondemnCollisionTerrain, true);
        ApiEventManager.OnMoveSuccess.AddListener(this, target, OnCondemnMoveSuccess, true);
        ApiEventManager.OnMoveFailure.AddListener(this, target, OnCondemnMoveFailure, true);
        ForceMovement(target, "RUN", pushEnd, CondemnPushSpeed, 0.0f, 0.0f, 0.0f,
                      movementType: ForceMovementType.FIRST_WALL_HIT,
                      movementOrdersType: ForceMovementOrdersType.CANCEL_ORDER,
                      movementOrdersFacing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING);
        //Particle for wall hit is this: "Vayne_WallHit_tar"
        //This is the damage ratio for wall hit: 45 + _vayne.Stats.AttackDamage.FlatBonus * 0.75f
    }

    private Vector2 GetCondemnTargetPoint(AttackableUnit target, Vector2 castPosition) {
        var from      = target.Position;
        var push      = from - castPosition;

        if (push.LengthSquared() < 0.0001f) {
            push = new Vector2(target.Direction.X, target.Direction.Z);
        }

        if (push.LengthSquared() < 0.0001f) {
            push = new Vector2(_vayne.Direction.X, _vayne.Direction.Z);
        }

        if (push.LengthSquared() < 0.0001f) {
            push = new Vector2(1.0f, 0.0f);
        }

        push = Vector2.Normalize(push);
        return from + push * CondemnPushDistance;
    }

    private void OnCondemnCollisionTerrain(GameObject unitObj) {
        if (unitObj is AttackableUnit unit) {
            _pendingWallHits.Add(unit.NetId);
        }
    }

    private void OnCondemnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters) {
        if (!_pendingWallHits.Remove(unit.NetId) || unit.IsDead) {
            return;
        }

        AddBuff("Stun", CondemnWallStunDuration, 1, _spell, unit, _vayne);
        AddParticleTarget(_vayne, unit, "Vayne_WallHit_tar", unit);
        unit.TakeDamage(_vayne, 45 + _vayne.Stats.AttackDamage.FlatBonus * 0.75f, DamageType.DAMAGE_TYPE_PHYSICAL,
                        DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
    }

    private void OnCondemnMoveFailure(AttackableUnit unit, ForceMovementParameters parameters) {
        _pendingWallHits.Remove(unit.NetId);
    }
}
