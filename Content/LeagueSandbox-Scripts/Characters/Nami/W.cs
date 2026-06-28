using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

// Nami W (Ebb and Flow) — replay-verified flow (ec72643482, 171 casts; memory:
// chain-bounce-spells):
//   ANS NamiW (cast shell, CastType=0, no own missile)
//   -> forced NamiWAlly/NamiWEnemy (by clicked target's allegiance) flies from Nami
//      (speed 2500, MissileGravity=80 lob visual); SELF-cast: instant heal instead,
//      first bounce (MisEnemy) launches directly from Nami.
//   -> engine alternating chain (BOTH bounce names set): each bounce targets the
//      OPPOSITE allegiance of the unit just hit, full spell switch to
//      NamiWMissileAlly/NamiWMissileEnemy (1500 speed), max 3 hits, no revisits.
//
// All hit logic lives HERE in one handler, registered on all four segment spells —
// the listener system keys on spell instances, not on owning scripts. The four
// missile scripts below are metadata-only declarations, mirroring Riot's per-spell
// server-lua structure where the stubs carry only declarative data.
public class NamiW : ISpellScript {
    private ObjAIBase      _nami;
    private Spell          _spell;
    private AttackableUnit _target;
    private bool           _selfCastPending;
    private bool           _chainListenersRegistered;

    // NamiW is the cast SHELL only (CastType=0) — the first missile is the forced
    // NamiWAlly/NamiWEnemy cast below. Declaring MissileParameters here would make
    // FinishCasting spawn an additional invisible NamiW chain missile -> double chain.
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nami  = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _nami, OnUpdateStats);
        // Chain listeners are registered lazily on first cast: spell scripts activate inside
        // the Spell constructor in SLOT ORDER (Q-R first, ExtraSpells 45+ last), so at
        // NamiW.OnActivate time GetSpell() for the segment spells still returns null.
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        // Once-flag is mandatory: AddListener does NOT dedupe — registering per cast
        // stacks the handlers, multiplying damage/heal with every W cast.
        if (!_chainListenersRegistered) {
            _chainListenersRegistered = true;
            ApiEventManager.OnSpellHit.AddListener(this, _nami.GetSpell("NamiWAlly"), OnSpellHit);
            ApiEventManager.OnSpellHit.AddListener(this, _nami.GetSpell("NamiWEnemy"), OnSpellHit);
            ApiEventManager.OnSpellHit.AddListener(this, _nami.GetSpell("NamiWMissileAlly"), OnSpellHit);
            ApiEventManager.OnSpellHit.AddListener(this, _nami.GetSpell("NamiWMissileEnemy"), OnSpellHit);
            ApiEventManager.OnLaunchMissile.AddListener(this, _nami.GetSpell("NamiWMissileEnemy"), OnLaunchMissile);
        }

        _target          = target;
        _selfCastPending = false;
    }

    public void OnSpellPostCast(Spell spell) {
        if (_target == _nami) {
            // Self-cast: no first missile (replay: 0 first-missile MISREPs on self-casts).
            ApplyAllyHit(_target, bounceIndex: 0, spell);

            // The chain still starts: first bounce (MisEnemy) launches directly from Nami.
            // Seeded via OnLaunchMissile so the self-heal counts as hit 1.
            // Champion-only, like every other bounce selection (replay-verified 245/245).
            var nextEnemy = GetUnitsInRange(_nami, _nami.Position, 450f, true,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)
                .OrderBy(x => Vector2.DistanceSquared(_nami.Position, x.Position))
                .FirstOrDefault();
            if (nextEnemy != null) {
                _selfCastPending = true;
                SpellCast(_nami, 3, SpellSlotType.ExtraSlots, true, nextEnemy, _nami.Position);
            }
        } else if (_target.Team == _nami.Team) {
            SpellCast(_nami, 2, SpellSlotType.ExtraSlots, true, _target, _nami.Position);
        } else {
            SpellCast(_nami, 6, SpellSlotType.ExtraSlots, true, _target, _nami.Position);
        }
    }

    // One handler for every chain segment (first missiles AND bounces). The chain-wide
    // missile.HitCount is already incremented at hit time -> bounceIndex = HitCount - 1.
    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        int bounceIndex = missile.HitCount - 1;
        if (target.Team == _nami.Team) {
            ApplyAllyHit(target, bounceIndex, spell);
        } else {
            ApplyEnemyHit(target, bounceIndex);
        }
    }

    private float BounceModifier(int bounceIndex) {
        return 1f + (-0.15f + 0.075f * (_nami.Stats.AbilityPower.Total / 100f)) * bounceIndex;
    }

    private void ApplyAllyHit(AttackableUnit target, int bounceIndex, Spell buffSource) {
        var ap   = _nami.Stats.AbilityPower.Total * 0.6f;
        var heal = (65f + 30f * (_spell.CastInfo.SpellLevel - 1) + ap) * BounceModifier(bounceIndex);

        target.TakeHeal(_nami, heal, HealType.SelfHeal);
        AddBuff("NamiPassive", 1.5f, 1, buffSource, target, _nami);
        AddParticleTarget(_nami, target, "Nami_Base_W_tar_ally", target);
        AddParticleTarget(_nami, target, "Nami_Base_W_heal", target);
    }

    private void ApplyEnemyHit(AttackableUnit target, int bounceIndex) {
        var ap  = _nami.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var dmg = (_spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] + ap) * BounceModifier(bounceIndex);
        
        AddParticleTarget(_nami, target, "Nami_Base_W_tar", target);
        target.TakeDamage(_nami, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                          DamageResultType.RESULT_NORMAL);
    }

    // Seeds the self-cast chain: the self-heal already consumed hit 1, so the first bounce
    // missile starts at HitCount=1 with Nami in ObjectsHit.
    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        if (!_selfCastPending) {
            return;
        }
        _selfCastPending = false;
        if (missile is SpellChainMissile chain) {
            chain.SetChainState(new List<GameObject> { _nami }, 1);
        }
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var bounceModifier = (-0.15f + 0.075f * (_nami.Stats.AbilityPower.Total / 100f)) * 100;
        SetSpellToolTipVar(_nami, 0, bounceModifier, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }
}

public class NamiWAlly : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Chained,
            // Replay-verified champion-only bounce rule (245/245 targets) — the JSON
            // flags would also allow minions/neutrals.
            BounceAffectsOverride = SpellDataFlags.AffectHeroes | SpellDataFlags.AffectEnemies | SpellDataFlags.AffectFriends
                | SpellDataFlags.IgnoreAllyMinion | SpellDataFlags.IgnoreEnemyMinion,
            BounceSpellNameAlly = "NamiWMissileAlly",
            BounceSpellNameEnemy = "NamiWMissileEnemy",
            MaximumHits = 3,
            BounceSelection =  BounceSelection.Nearest,
        },
        TriggersSpellCasts = false,
    };
}

public class NamiWEnemy : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Chained,
            // Replay-verified champion-only bounce rule (245/245 targets) — the JSON
            // flags would also allow minions/neutrals.
            BounceAffectsOverride = SpellDataFlags.AffectHeroes | SpellDataFlags.AffectEnemies | SpellDataFlags.AffectFriends
                | SpellDataFlags.IgnoreAllyMinion | SpellDataFlags.IgnoreEnemyMinion,
            BounceSpellNameAlly = "NamiWMissileAlly",
            BounceSpellNameEnemy = "NamiWMissileEnemy",
            MaximumHits = 3,
            BounceSelection =  BounceSelection.Nearest,
        },
        TriggersSpellCasts = false,
    };
}

public class NamiWMissileAlly : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Chained,
            // Replay-verified champion-only bounce rule (245/245 targets) — the JSON
            // flags would also allow minions/neutrals.
            BounceAffectsOverride = SpellDataFlags.AffectHeroes | SpellDataFlags.AffectEnemies | SpellDataFlags.AffectFriends
                | SpellDataFlags.IgnoreAllyMinion | SpellDataFlags.IgnoreEnemyMinion,
            BounceSpellNameAlly = "NamiWMissileAlly",
            BounceSpellNameEnemy = "NamiWMissileEnemy",
            MaximumHits = 3,
            BounceSelection =  BounceSelection.Nearest,
        },
        TriggersSpellCasts = false,
    };
}

public class NamiWMissileEnemy : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Chained,
            // Replay-verified champion-only bounce rule (245/245 targets) — the JSON
            // flags would also allow minions/neutrals.
            BounceAffectsOverride = SpellDataFlags.AffectHeroes | SpellDataFlags.AffectEnemies | SpellDataFlags.AffectFriends
                | SpellDataFlags.IgnoreAllyMinion | SpellDataFlags.IgnoreEnemyMinion,
            BounceSpellNameAlly = "NamiWMissileAlly",
            BounceSpellNameEnemy = "NamiWMissileEnemy",
            MaximumHits = 3,
            BounceSelection =  BounceSelection.Nearest,
        },
        TriggersSpellCasts = false,
    };
}
