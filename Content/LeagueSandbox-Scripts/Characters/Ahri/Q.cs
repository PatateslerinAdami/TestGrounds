using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using System;
using System.Collections.Generic;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AhriOrbofDeception : ISpellScript
{
    private ObjAIBase _ahri;
    private Vector2 _start, _end;


    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
        IsDamagingSpell = true,
        SpellDamageRatio = 0.5f
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ahri = owner;
        // No OnSpellHit / damage here: S1's AhriOrbofDeception is a pure launcher (SelfExecute →
        // BBSpellCast of the orb missile). All Q damage now lives in the AhriOrbDamage /
        // AhriOrbDamageSilence buffs that the missiles apply on hit.
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _start = start;
        _end = end;
    }

    public void OnSpellCast(Spell spell)
    {
        // S1 SelfExecute: face the cursor, then fire the orb at a point a FIXED 900u ahead in that
        // facing direction (BBFaceDirection + BBGetPointByUnitFacingOffset, Distance = 900) — not
        // the raw cursor pos. OverrideForceLevel = Q's rank (S1: OverrideForceLevelVar = "Level");
        // the wire SpellLevel of the orb MISREPs mirrors the champion Q's rank, not the unleveled
        // extra slot (replay 9c0533a1: outbound + return both carry the Q level).
        FaceDirection(_end, _ahri, true);
        var aim = GetPointByUnitFacingOffset(_ahri, 900f);
        SpellCast(_ahri, 0, SpellSlotType.ExtraSlots, _start, aim, true, Vector2.Zero,
            overrideForceLevel: spell.CastInfo.SpellLevel);
    }
}

public class AhriOrbMissile : ISpellScript
{
    private ObjAIBase _ahri;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Arc,
            // Riot launches the orb from the R_hand bone, ~100u above ground — the 4.20 data's
            // MissileTargetHeightAugment is 0, the height comes from the server's bone sample.
            // Replay 9c0533a1 (4.20): all 42 outbound spawn MISREPs carry Position.Y =
            // terrain + 100 and Velocity.Y = -277.8 = -100 / (900u / 2500); the client takes
            // its flight height from wire Position.Y (OverridePlacement: mStartHeightFromGround
            // = Position.y - terrain), so without this the orb drags along the ground.
            OverrideHeightAugment = 50f
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ahri = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        // S1 AhriOrbMissile TargetExecute: add the AhriOrbDamage (magic) buff to each unit the orb
        // passes through. originSpell = the champ Q (Spells[0]) so the buff can read Q level + coeff.
        // ENGINE DETOUR: the automatic spell-shield gate lives in Spell.ApplyEffects, which we no
        // longer route through — so we replicate S1's explicit BBBreakSpellShields here. Returns
        // false (and skips the buff) when a shield consumes the hit.
        if (BreakSpellShields(target, _ahri.Spells[0]))
        {
            AddBuff("AhriOrbDamage", 2f, 1, _ahri.Spells[0], target, _ahri);
        }
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnSpellMissileEnd);
    }

    private void OnSpellMissileEnd(SpellMissile missile)
    {
        // S1 SpellOnMissileEnd: everyone in a 100u circle at the orb's turn point eats the outbound
        // magic hit (covers a cluster sitting exactly at max range). Buff-dedup means units already
        // hit in flight just get a refresh, no second instance. Per-unit shield gate like the
        // fly-through path (can't use ...AddBuff here — it has no shield check).
        var atEnd = ForEachUnitInTargetArea(_ahri, missile.Position, 100f,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral
                                         | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var u in atEnd)
        {
            if (BreakSpellShields(u, _ahri.Spells[0]))
            {
                AddBuff("AhriOrbDamage", 2f, 1, _ahri.Spells[0], u, _ahri);
            }
        }

        // isForceCastingOrChanneling: Riot's return MISREP carries CastInfo bits = 12
        // (ForceCast|OverrideCastPos, replay 9c0533a1) — S1's BBSpellCast sets
        // ForceCastingOrChannelling = true. Force level = Q's rank (OverrideForceLevelVar).
        SpellCast(_ahri, _ahri.IsDead ? 6 : 1, SpellSlotType.ExtraSlots, true, _ahri, missile.Position,
            isForceCastingOrChanneling: true,
            overrideForceLevel: _ahri.Spells[0].CastInfo.SpellLevel);

        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnSpellMissileEnd);
        ApiEventManager.OnLaunchMissile.RemoveListener(this, _spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.RemoveListener(this, _spell, OnSpellHit);
    }
}

public class AhriOrbReturn : ISpellScript
{
    private ObjAIBase _ahri;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        PersistsThroughDeath = true,
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Arc,
            // The return orb spawns at the outbound orb's 3D death position — terrain + ~100
            // in all 40 return spawn MISREPs of replay 9c0533a1 (the outbound flies at +100,
            // see AhriOrbMissile). Wire Position.Y is what the client flies at; tracked
            // missiles (LineMissileTrackUnits=1) get Velocity.Y = 0, which our packet builder
            // already handles.
            OverrideHeightAugment = 50f
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ahri = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        // NO OnSpellMissileHit removal here: the catch is the engine's tracked-ARRIVAL
        // (SpellLineMissile re-aims Destination at Ahri every tick, Move snaps onto her
        // center → SetToRemove WITH the destroy packet — Riot sends exactly that destroy,
        // 40/40 returns in replay 9c0533a1). Killing the missile on the swept-collision
        // hit instead fired at LineWidth(100) + Ahri's radius ≈ 165u BEFORE her center,
        // yanking the client orb visibly early — the 4.20 client flies its tracked copy
        // all the way to the unit's position (decomp: CheckAtEndPoint plane-crossing on
        // the per-frame re-aimed endpoint, no radius shortcut).
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnSpellMissileEnd);
    }

    private void OnSpellMissileEnd(SpellMissile missile)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        // S1 AhriOrbReturn TargetExecute gates on Target != Attacker — the spell's AlwaysSelf
        // flag (0x40000 in Flags=377856) makes Ahri herself a VALID target of her homing return
        // orb, so without this gate she eats her own return TRUE damage on every catch. (This
        // fires at collision contact, ~165u before the tracked-arrival that ends the missile.)
        if (target == _ahri)
        {
            return;
        }

        // S1 AhriOrbReturn TargetExecute: the return orb applies AhriOrbDamageSilence (TRUE damage)
        // — separate buff name from the outbound orb so a unit caught by both takes both hits.
        if (BreakSpellShields(target, spell))
        {
            AddBuff("AhriOrbDamageSilence", 2f, 1, _ahri.Spells[0], target, _ahri);
        }
    }
}

public class AhriOrbReturnDead : ISpellScript
{
    private ObjAIBase _ahri;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Arc,
            // Same orb, same spawn point (outbound death position at terrain + 100) as the live
            // return — no wire samples of the dead variant in the corpus, height mirrored from
            // AhriOrbReturn.
            OverrideHeightAugment = 50f
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ahri = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnSpellMissileEnd);
    }

    private void OnSpellMissileEnd(SpellMissile missile)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }


    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        // Dead-caster return orb: same TRUE-damage buff as the live return.
        if (BreakSpellShields(target, _ahri.Spells[0]))
        {
            AddBuff("AhriOrbDamageSilence", 2f, 1, _ahri.Spells[0], target, _ahri);
        }
    }
}