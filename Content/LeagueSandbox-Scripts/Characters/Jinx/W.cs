using System;
using System.Numerics;
using Buffs;
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
using static GameServerCore.Content.HashFunctions;

namespace Spells;

public class JinxW : ISpellScript {
    private Vector2      _targetPos;
    private SpellMissile _missile;

    private ObjAIBase _jinx;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle
        },
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jinx  = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnStatsUpdate);
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnMissileLaunch);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) {
        ApiEventManager.OnUpdateStats.RemoveListener(this, owner, OnStatsUpdate);
        ApiEventManager.OnLaunchMissile.RemoveListener(this, spell, OnMissileLaunch);
    }

    public void OnMissileLaunch(Spell spell, SpellMissile missile) {
        _missile = missile;
        AddParticleTarget(_jinx, _jinx, "Jinx_W_Beam", _missile, 5f, bone: "spine", targetBone: "root");
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _targetPos                      = end;
        FaceDirection(_targetPos, _jinx, true);
        var duration = _jinx.Stats.AttackSpeedMultiplier.FlatBonus switch {
            > 2.5f  => 0.4f,
            > 2.25f => 0.42f,
            > 2f    => 0.44f,
            > 1.75f => 0.46f,
            > 1.5f  => 0.48f,
            > 1.25f => 0.5f,
            > 1f    => 0.52f,
            > 0.75f => 0.54f,
            > 0.5f  => 0.56f,
            > 0.25f => 0.58f,
            _       => 0.6f
        };
        AddBuff("JinxWLockout", duration, 1, spell, _jinx, _jinx);
    }

    public void OnSpellPostCast(Spell spell) {
        SpellCast(_jinx, 6, SpellSlotType.ExtraSlots, _targetPos, _targetPos, false, Vector2.Zero);
        _missile?.SetToRemove();
    }

    private void OnStatsUpdate(AttackableUnit unit, float diff) {
        var bonusAd = _jinx.Stats.AttackDamage.Total * _spell.SpellData.Coefficient;
        SetSpellToolTipVar(_jinx, 2, bonusAd, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }
}

public class JinxWMissile : ISpellScript {
    private ObjAIBase _jinx;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle
        },
        TriggersSpellCasts = false,
        IsDamagingSpell    = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jinx = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var duration = _jinx.Stats.AttackSpeedMultiplier.FlatBonus switch {
            > 2.5f  => 0.4f,
            > 2.25f => 0.42f,
            > 2f    => 0.44f,
            > 1.75f => 0.46f,
            > 1.5f  => 0.48f,
            > 1.25f => 0.5f,
            > 1f    => 0.52f,
            > 0.75f => 0.54f,
            > 0.5f  => 0.56f,
            > 0.25f => 0.58f,
            _       => 0.6f
        };
        PlayAnimation(_jinx, "Spell2", 1f - 0.6f - duration);
        spell.CastInfo.DesignerCastTime  = duration;
        spell.CastInfo.DesignerTotalTime = duration + spell.SpellData.ChannelDuration[spell.CastInfo.SpellLevel];
    }

    public void OnSpellPostCast(Spell spell) { SetStatus(_jinx, StatusFlags.Rooted, false); }

    public void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ad     = _jinx.Stats.AttackDamage.Total * spell.SpellData.Coefficient;
        var damage = 10 + 50 * (spell.CastInfo.SpellLevel - 1) + ad;

        AddParticleTarget(_jinx, target, "Jinx_W_Tar", target);
        target.TakeDamage(_jinx, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);

        var variables      = new BuffVariables();
        variables.Set("slowPercent", 0.3f + 0.1f * (_jinx.GetSpell("JinxW").CastInfo.SpellLevel - 1));
        AddBuff("Slow", 2f, 1, spell, target, _jinx, buffVariables: variables);
        AddBuff("JinxWSight", 2f, 1, spell, target, _jinx);

        missile.SetToRemove();
    }
}
