using System.Numerics;
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

public class Headbutt : ISpellScript {
    private ObjAIBase _alistar;
    private AttackableUnit _target;
    private Spell _spell;
    // Alistar's position at cast time (before the charge). Used as the knockback's away-from anchor,
    // because after the charge Alistar stands on the target and his live position is degenerate.
    private Vector2 _castOrigin;

    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = false,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _alistar = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        SpellEffectCreate("HeadButt_tar.troy", _alistar, _target, null, orientTowards: _target.GetPosition3D(), boneName: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.UpdateOrientation, keywordObject: _alistar, scale: 1f);
        var ap = _alistar.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var dmg = _spell.SpellData.EffectLevelAmount[2][_spell.CastInfo.SpellLevel] + ap;
        target.TakeDamage(_alistar, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
            DamageResultType.RESULT_NORMAL);
        var buffVars = new VariableTable();
        buffVars.Set("castOriginX", _castOrigin.X);
        buffVars.Set("castOriginY", _castOrigin.Y);
        AddBuff("HeadbuttTarget", 0.75f, 1, _spell, target, _alistar, variableTable: buffVars);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        _spell = spell;
    }

    public void OnSpellPostCast(Spell spell)
    {
        FaceDirection(_target.Position, _alistar);

        _castOrigin = _alistar.Position;
        var distance = Vector2.Distance(_alistar.Position, _target.Position);
        var timeScale = System.Math.Clamp(distance / 650f, 0.25f, 0.9f);
        PlayAnimation(_alistar, "Spell2", timeScale, 0,0,AnimationFlags.NoBlend | AnimationFlags.Junk5 | AnimationFlags.Junk6 | AnimationFlags.Junk7);
        ApiEventManager.OnMoveSuccess.AddListener(this, _alistar, OnMoveSuccess);
        ForceMove(_alistar, _target.Position, 1500, 2, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, orders: ForceMovementOrdersType.CANCEL_ORDER,idealDistance: distance, movementName:"headbuttDash");
        
    }

    private void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
    {
        if (parameters.MovementName != "headbuttDash") return;
        if (!IsUnitInRange(_target, unit.Position, 400f, true)) return;
        _spell.ApplyEffects(_target);
        ApiEventManager.OnMoveSuccess.RemoveListener(this, _alistar, OnMoveSuccess);
    }
}