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
        // the raw cursor pos.
        FaceDirection(_end, _ahri, true);
        var aim = GetPointByUnitFacingOffset(_ahri, 900f);
        SpellCast(_ahri, 0, SpellSlotType.ExtraSlots, _start, aim, true, Vector2.Zero);
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
            Type = MissileType.Arc
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

        if (_ahri.IsDead)
        {
            SpellCast(_ahri, 6, SpellSlotType.ExtraSlots, missile.Position, _ahri.Position, true, missile.Position);
        }
        else
        {
            SpellCast(_ahri, 1, SpellSlotType.ExtraSlots, true, _ahri, missile.Position);
        }

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
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Arc
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
        ApiEventManager.OnSpellMissileHit.AddListener(this, missile, OnSpellMissileHit);
    }

    private void OnSpellMissileEnd(SpellMissile missile)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    private void OnSpellMissileHit(SpellMissile missile, AttackableUnit target)
    {
        if (target != _ahri) return;
        ApiEventManager.RemoveAllListenersForOwner(this);
        missile.SetToRemove();
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        // S1 AhriOrbReturn TargetExecute: the return orb applies AhriOrbDamageSilence (TRUE damage)
        // — separate buff name from the outbound orb so a unit caught by both takes both hits.
        if (BreakSpellShields(target, _ahri.Spells[0]))
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
          Type = MissileType.Arc
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