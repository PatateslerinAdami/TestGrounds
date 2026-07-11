using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class JaxLeapStrike : ISpellScript
{
    private ObjAIBase _jax;
    private Spell _spell;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = false,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _jax = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _jax, OnUpdateStats);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var ad = _jax.Stats.AttackDamage.FlatBonus * spell.SpellData.Coefficient;
        var ap = _jax.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var damage = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ad + ap;

        if (!IsValidTarget(_jax, target,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectNeutral)) return;
        SpellEffectCreate("jax_leapstrike_tar.troy", _jax, target, null, keywordObject: _jax, flags: FXFlags.None);
        target.TakeDamage(_jax, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false);
        _jax.SetTargetUnit(target);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        _jax.StopMovement();
        var distance = Vector2.Distance(_jax.Position, _target.Position);
        var timeScale = 1f;
        if (distance < 150)
        {
            timeScale = 0.5f;
            FaceDirection(_target.Position, _jax, true);
        }
        else
        {
            FaceDirection(GetMovePositionByCollisionOffset(_jax, _target), _jax, true);
        }

        SpellEffectCreate("jax_immortal_Q_cas.troy", _jax, _jax, _jax, boneName: "C_Buffbone_Glb_Chest_Loc",
            flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("jax_immortal_Q_cas_02.troy", _jax, _jax, _jax, flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("jax_immortal_Q_cas_03.troy", _jax, _jax, _jax, boneName: "Weapon",
            flags: FXFlags.SimulateWhileOffScreen);
        PlayAnimation(_jax, "Spell2", timeScale, flags: AnimationFlags.NoBlend | AnimationFlags.Junk5 | AnimationFlags.Junk6 | AnimationFlags.Junk7);
    }

    public void OnSpellCast(Spell spell)
    {
        
    }

    public void OnSpellPostCast(Spell spell)
    {
        float gravityVar;
        float speedVar;
        var distance = Vector2.Distance(_jax.Position, _target.Position);
        switch (distance)
        {
            case >= 600:
                gravityVar = 50;
                speedVar = 1650;
                break;
            case >= 500:
                gravityVar = 60;
                speedVar = 1500;
                break;
            case >= 400:
                gravityVar = 70;
                speedVar = 1350;
                break;
            case >= 300:
                gravityVar = 80;
                speedVar = 1300;
                break;
            case >= 200:
                gravityVar = 90;
                speedVar = 1200;
                break;
            case >= 100:
                gravityVar = 150;
                speedVar = 1100;
                break;
            default:
                gravityVar = 500;
                speedVar = 1100;
                break;
        }

        FaceDirection(distance <= 150f ? _target.Position : GetMovePositionByCollisionOffset(_jax, _target), _jax, true);

        ApiEventManager.OnMoveFailure.AddListener(this, _jax, OnMoveFailure);
        ApiEventManager.OnMoveSuccess.AddListener(this, _jax, OnMoveSuccess);
        ForceMove(_jax, GetMovePositionByCollisionOffset(_jax, _target), speedVar, gravity: gravityVar,
            ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, false, true,
            ForceMovementOrdersType.CANCEL_ORDER, "JaxLeapStrike");
    }

    private void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters)
    {
        if (parameters.MovementName != "JaxLeapStrike") return;
        _spell.ApplyEffects(_target);
        ApiEventManager.OnMoveSuccess.RemoveListener(this, _jax, OnMoveSuccess);
        ApiEventManager.OnMoveFailure.RemoveListener(this, _jax, OnMoveFailure);
    }

    private void OnMoveFailure(AttackableUnit unit, ForceMovementParameters parameters)
    {
        if (parameters.MovementName != "JaxLeapStrike") return;
        ApiEventManager.OnMoveSuccess.RemoveListener(this, _jax, OnMoveSuccess);
        ApiEventManager.OnMoveFailure.RemoveListener(this, _jax, OnMoveFailure);
    }

    private void OnUpdateStats(AttackableUnit owner, float diff)
    {
        var ad = _jax.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        var ap = _jax.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        SetSpellToolTipVar(_jax, 0, ad, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_jax, 1, ap, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}

public class JaxLeapStrikeAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
    };
}