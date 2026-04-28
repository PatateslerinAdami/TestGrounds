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
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class JaxLeapStrike : ISpellScript {
    private ObjAIBase      _jax;
    private Spell          _spell;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true,
        CastTime             = 0f,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jax   = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _jax, OnUpdateStats);
        ApiEventManager.OnMoveSuccess.AddListener(this, owner, OnMoveEnd);
        //ApiEventManager.OnMoveFailure.AddListener(this, owner, OnMoveFailure);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        _jax.StopMovement();
    }

    public void OnSpellCast(Spell spell) {
        var distance  = Vector2.Distance(_jax.Position, _target.Position);
        var   timeScale = 1f;
        if (distance < 150) {
            timeScale = 0.5f;
            FaceDirection(_target.Position, _jax, true);
        } else { FaceDirection(GetPositionByOffset(0f, -150f), _jax, true); }

        PlayAnimation(_jax, "Spell2", timeScale);
    }

    public void OnSpellPostCast(Spell spell) {
        float gravityVar;
        float speedVar;
        var distance = Vector2.Distance(_jax.Position, _target.Position);
        switch (distance) {
            case >= 600:
                gravityVar = 50;
                speedVar   = 1650;
                break;
            case >= 500:
                gravityVar = 60;
                speedVar   = 1500;
                break;
            case >= 400:
                gravityVar = 70;
                speedVar   = 1350;
                break;
            case >= 300:
                gravityVar = 80;
                speedVar   = 1300;
                break;
            case >= 200:
                gravityVar = 90;
                speedVar   = 1200;
                break;
            case >= 100:
                gravityVar = 150;
                speedVar   = 1100;
                break;
            default:
                gravityVar = 500;
                speedVar   = 1100;
                break;
        }

        FaceDirection(distance <= 150f ? _target.Position : GetPositionByOffset(0f, -150f), _jax, true);

        _jax.DashToLocation(GetPositionByOffset(0f, -150f), speedVar, "", gravityVar,
                            keepFacingLastDirection: false);
    }

    private Vector2 GetPositionByOffset(float angleOffset, float distanceOffset) {
        var toTarget = _target.Position - _jax.Position;
        if (toTarget.LengthSquared() <= float.Epsilon) { return _target.Position; }

        var direction = Vector2.Normalize(toTarget);
        var angleRad  = angleOffset * (MathF.PI / 180.0f);
        var sin       = MathF.Sin(angleRad);
        var cos       = MathF.Cos(angleRad);
        var rotatedDirection = new Vector2(
            direction.X * cos - direction.Y * sin,
            direction.X * sin + direction.Y * cos
        );

        return _target.Position + rotatedDirection * distanceOffset;
    }

    private void OnMoveEnd(AttackableUnit unit, ForceMovementParameters parameters) {
        var ad     = _jax.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        var ap     = _jax.Stats.AbilityPower.Total     * _spell.SpellData.Coefficient;
        var damage = 70f + 40f * (_spell.CastInfo.SpellLevel - 1) + ad + ap;

        if (!IsValidTarget(_jax, _target,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectNeutral)) return;
        AddParticleTarget(_jax, _target, "jax_leapstrike_tar", _target);
        if (_jax.HasBuff("JaxEmpowerTwo")) {
            var empowerAp = _jax.Stats.AbilityPower.Total * _jax.GetSpell("JaxEmpowerTwo").SpellData.Coefficient;
            var dmg       = 40f + 35f * (_jax.GetSpell("JaxEmpowerTwo").CastInfo.SpellLevel - 1) + empowerAp;
            damage += dmg;
            RemoveBuff(_jax, "JaxEmpowerTwo");
            AddParticleTarget(_jax, _target, "EmpowerTwoHit_tar", _target);
        }
        _target.TakeDamage(_jax, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false);
        _jax.SetTargetUnit(_target);
    }

    private void OnUpdateStats(AttackableUnit owner, float diff) {
        var ad = _jax.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        var ap = _jax.Stats.AbilityPower.Total     * _spell.SpellData.Coefficient;
        SetSpellToolTipVar(_jax, 0, ad, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_jax, 1, ap, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}

public class JaxLeapStrikeAttack : ISpellScript {
    private ObjAIBase _jax;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _jax = owner; }

    public void OnSpellPostCast(Spell spell) { _jax.ResetAutoAttackSpell(); }
}