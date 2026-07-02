using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class DianaTeleport : ISpellScript {
    private  ObjAIBase _diana;
    private AttackableUnit _target;
    private Spell _spell;
    private Particle _dashParticle;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _diana = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        _spell = spell;
        
    }

    public void OnSpellPostCast(Spell spell)
    {
        _dashParticle = AddParticleTarget(_diana, _diana, "Diana_Base_R_Cas.troy", _diana, flags: FXFlags.SimulateWhileOffScreen);
        ApiEventManager.OnMoveSuccess.AddListener(this, _diana, OnMoveSuccess);
        ApiEventManager.OnMoveFailure.AddListener(this, _diana, OnMoveFailure);
        FaceDirection(_target.Position, _diana, true);
        if (_target.HasBuff("Moonlight"))
        {
            spell.SetCooldown(0f, true);
            var units = EnumerateUnitsInRange(_diana.Position, 2500000f, true).Where(unit => unit.HasBuff("Moonlight"));
            foreach (var unit in units)
            {
                RemoveBuff(unit, "Moonlight");
            }
        }
        ForceMove(_diana, _target.Position, 2500f, 0, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, true, true, ForceMovementOrdersType.CANCEL_ORDER, movementName: "DianaTeleport");
        //ForceMoveToUnit(_diana, _target, 2100f, 150f,0f, 2100f, 4f, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, true, ForceMovementOrdersType.CANCEL_ORDER, "DianaTeleport");
    }

    private void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
    {
        if (parameters.MovementName != "DianaTeleport") return;
        RemoveParticle(_dashParticle);
        var ap = _diana.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var dmg = _spell.SpellData.EffectLevelAmount[2][_spell.CastInfo.SpellLevel] + ap;
        AddParticleTarget(_diana, _diana, "Diana_Base_R_End.troy", _diana);
        AddParticleTarget(_diana, _target, "Diana_Base_R_Tar.troy", _target);
        _target.TakeDamage(_diana, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
        ApiEventManager.OnMoveSuccess.RemoveListener(this, _diana, OnMoveSuccess);
        ApiEventManager.OnMoveEnd.RemoveListener(this, _diana, OnMoveFailure);
    }

    private void OnMoveFailure(AttackableUnit unit, ForceMovementParameters parameters)
    {
        ApiEventManager.OnMoveSuccess.RemoveListener(this, _diana, OnMoveSuccess);
        ApiEventManager.OnMoveEnd.RemoveListener(this, _diana, OnMoveFailure);
    }
}