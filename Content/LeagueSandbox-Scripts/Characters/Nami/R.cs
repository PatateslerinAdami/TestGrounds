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

namespace Spells;

public class NamiR : ISpellScript {
    private ObjAIBase _nami;
    private Vector2   _endPos;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        NotSingleTargetSpell = true,
        AutoFaceDirection = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _endPos = end;
    }

    public void OnSpellCast(Spell spell) {
        PlayAnimation(_nami, "Spell4");
    }

    public void OnSpellPostCast(Spell spell) {
        SpellCast(_nami, 0, SpellSlotType.ExtraSlots, _endPos, _endPos, true, Vector2.Zero);
    }
}

public class NamiRMissile : ISpellScript {
    private ObjAIBase _nami;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type                          = MissileType.Circle,
            CanHitSameTargetConsecutively = false,
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var   ap       = _nami.Stats.AbilityPower.Total * 0.6f;
        var   dmg      = 150f + 100f * (_nami.GetSpell("NamiR").CastInfo.SpellLevel - 1) + ap;
        float distance = Vector2.Distance(target.Position, new Vector2(spell.CastInfo.SpellCastLaunchPosition.X, spell.CastInfo.SpellCastLaunchPosition.Y));
        float slowDuration    = 2f + 2f * (Math.Min(distance, 2750f) / 2750f);


        
        if (IsValidTarget(_nami, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) {
            target.TakeDamage(_nami, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                              DamageResultType.RESULT_NORMAL);
            
            //slow
            var variables      = new BuffVariables();
            variables.Set("slowAmount", 0.5f + 0.1f * (_nami.GetSpell("NamiR").CastInfo.SpellLevel - 1));
            AddBuff("Slow", slowDuration, 1, spell, target, _nami, buffVariables:  variables);
            
            AddBuff("NamiRVision", 0.5f, 1, spell, target, _nami);
        }else if (IsValidTarget(_nami, target, SpellDataFlags.AffectFriends | SpellDataFlags.AffectHeroes)) {
            AddBuff("NamiPassive",         1.5f, 1, spell, target, _nami);
        }
    }
}