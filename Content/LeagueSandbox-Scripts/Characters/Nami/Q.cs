using System;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
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
        IsDeathRecapSource = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var ap      = _nami.Stats.AbilityPower.Total * 0.5f;
        var dmg     = 75f + (55f * _nami.GetSpell("NamiQ").CastInfo.SpellLevel - 1) + ap;
        AddBuff("NamiQDebuff", 1.5f, 1, spell, target, _nami);
        target.TakeDamage(_nami, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);
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

        var bubbleMinion = AddMinion(_nami, "TestCubeRender10Vision", "bubble", _endPos, _nami.Team, 0, true, false, isVisible: false,
            rooted: true, invulnerable: true, magicImmune: true);
        SetStatus(bubbleMinion, StatusFlags.Ghosted, true);
        // Replay-verified (ec72643482, 93 Q casts): Riot casts NamiQMissile as a REAL spell —
        // CastSpellAns(NamiQMissile, slot 50, targets=1) + ForceCreateMissile in the SAME tick
        // at Q's windup end (+230..260ms; the missile spell is InstantCast). NOT a script-spawn.
        // fireWithoutCasting=true (no CastSpellAns/FCM) leaves the client's NamiQ cast frame
        // unexecuted -> the client silently rejects every second Q press (no cd, no anim)
        // until the rejection/a move order clears the stale frame ("alternating Q" bug).
        SpellCast(_nami, 5, SpellSlotType.ExtraSlots, false, bubbleMinion, Vector2.Zero);

        // NOTE: Riot also sends NPC_InstantStop_Attack in this tick on ~44% of Q casts
        // (41/93 tight-window vs 4/93 base rate; NOT auto-attack-related — 37/41 had no
        // prior AA). The trigger condition is server-internal and not visible in any of our
        // artifacts (client decomp has only the receive side, no Lua API exists), so we
        // deliberately DON'T send it until the condition is understood — an unconditional
        // send would be guessing. See memory: nami-q-wire-and-cast-lockout.
    }
}

public class NamiQMissile : ISpellScript {
    private ObjAIBase _nami;
    private Spell     _spell;
    
    // TriggersSpellCasts=true: the real-cast pipeline sends CastSpellAns + (HasClientCastInfo)
    // ForceCreateMissile — the client needs that FCM to execute its open NamiQ cast frame
    // (see comment in NamiQ.OnSpellPostCast; Riot wire shows exactly this pattern).
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami = owner;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    // Constant ~0.7s travel time comes from NamiQMissile.json MissileFixedTravelTime=0.7 —
    // the missile factory computes speed = dist / 0.7 BEFORE creation (replay: 98 casts,
    // distance 82..849u with constant landing delay). No post-spawn SetSpeed needed; the
    // client spawns its own missile from the same data via the FCM path anyway.
    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        ApiEventManager.OnSpellMissileHit.AddListener(this, missile, OnMissileHit, true);
    }
    
    private void OnMissileHit(SpellMissile missile, AttackableUnit target) {
        var center  = missile.Position;
        AddParticlePos(_nami, "Nami_Base_Q_pop", center, center);
        var enemies = GetUnitsInRange(_nami, center, 200f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var enemy in enemies) {
            _nami.Spells[0].ApplyEffects(enemy);
        }
        var allies = GetUnitsInRange(_nami, center, 225f, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectHeroes);
        foreach (var ally in allies) {
            AddBuff("NamiPassive", 1.5f, 1, _spell, ally, _nami);
        }
        
        target.Die(CreateDeathData(false, 0, target, target, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0));
        ApiEventManager.OnLaunchMissile.RemoveListener(this, _spell, OnLaunchMissile);
        ApiEventManager.OnSpellMissileHit.RemoveListener(this, missile, OnMissileHit);
    }
}
