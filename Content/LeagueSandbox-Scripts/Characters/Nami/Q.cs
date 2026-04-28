using System;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class NamiQ : ISpellScript {
    private ObjAIBase _nami;
    private Vector2   _endPos;
    private const float BubbleDelay = 0.725f;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _endPos = end;
    }

    public void OnSpellPostCast(Spell spell) {
        AddParticle(_nami, null, "Nami_Base_Q_indicator_green", _endPos, BubbleDelay, 1f, teamOnly: _nami.Team);
        switch (_nami.Team) {
            case TeamId.TEAM_BLUE:
                AddParticle(_nami, null, "Nami_Base_Q_indicator_red", _endPos, BubbleDelay, 1f, teamOnly: TeamId.TEAM_PURPLE);
                break;
            case TeamId.TEAM_PURPLE:
                AddParticle(_nami, null, "Nami_Base_Q_indicator_red", _endPos, BubbleDelay, 1f, teamOnly: TeamId.TEAM_BLUE);
                break;
        }
        SpellCast(_nami, 5, SpellSlotType.ExtraSlots, _endPos, _endPos, true, Vector2.Zero);
        SpellCast(_nami, 1, SpellSlotType.ExtraSlots, _endPos, _endPos, true, Vector2.Zero);
    }
}

public class NamiQDummyMissile : ISpellScript {
    private ObjAIBase _nami;
    private Spell     _spell;
    private const float BubbleDelay = 0.725f;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _spell = spell;
        var missile = spell.CreateSpellMissile(new MissileParameters {
            Type = MissileType.Arc,
            OverrideEndPosition = end
        });
        if (missile == null) return;
        var distance = Vector2.Distance(missile.Position, end);
        if (distance > 0.0f) missile.SetSpeed(distance / BubbleDelay);
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);
    }

    public void OnMissileEnd(SpellMissile missile) {
        var center  = missile.Position;
        var ap      = _nami.Stats.AbilityPower.Total * 0.5f;
        var dmg     = 75f + (55f * _nami.GetSpell("NamiQ").CastInfo.SpellLevel - 1) + ap;
        var enemies = GetUnitsInRange(_nami, center, 200f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var enemy in enemies) {
            AddBuff("NamiQDebuff", 1.5f, 1, _spell, enemy, _nami);
            enemy.TakeDamage(_nami, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                             DamageResultType.RESULT_NORMAL);
        }
        var allies = GetUnitsInRange(_nami, center, 225f, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectHeroes);
        foreach (var ally in allies) {
            AddBuff("NamiPassive", 1.5f, 1, _spell, ally, _nami);
        }
    }
}

public class NamiQMissile : ISpellScript {
    private ObjAIBase _nami;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Arc
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
    }
}
