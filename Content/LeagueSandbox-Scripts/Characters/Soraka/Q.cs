using System;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SorakaQ : ISpellScript
{
    private ObjAIBase _owner;
    private Vector2   _targetPos;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _targetPos = new Vector2(end.X, end.Y);
    }

    public void OnSpellCast(Spell spell)
    {
        // Cast VFX on caster
        AddParticleTarget(_owner, _owner, "Soraka_Base_Q_Cas.troy", _owner, lifetime: 0.8f);
        AddParticleTarget(_owner, _owner, "Soraka_Base_Q_Cast_Hand.troy", _owner,
            lifetime: 0.8f, bone: "R_hand");
    }

    public void OnSpellPostCast(Spell spell)
    {
        // Cast the missile to the ground target — client renders Soraka_Base_Q_Mis.troy
        SpellCast(_owner, 6, SpellSlotType.ExtraSlots, _targetPos, _targetPos, true, Vector2.Zero);
    }

    public void OnUpdate(float diff) { }
}

public class SorakaQMissile : ISpellScript
{
    private const float OuterRadius = 300f;
    private const float InnerRadius = 110f;

    private ObjAIBase _owner;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Circle
        },
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    public void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        var pos = missile.Position;
        var qSpell = _owner.GetSpell("SorakaQ");
        int rank = qSpell.CastInfo.SpellLevel;
        float ap = _owner.Stats.AbilityPower.Total * 0.35f;

        float[] baseDmg = { 0, 70, 110, 150, 190, 230 };
        float dmg = baseDmg[rank] + ap;

        // Impact VFX
        AddParticle(_owner, null, "Soraka_Base_Q_Tar.troy", pos, 2f);

        int champHits = 0;
        var enemies = GetUnitsInRange(_owner, pos, OuterRadius, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);

        foreach (var u in enemies)
        {
            if (u.Team == _owner.Team) continue;

            float dist = Vector2.Distance(pos, u.Position);
            float finalDmg = dist <= InnerRadius ? dmg * 1.5f : dmg;

            u.TakeDamage(_owner, finalDmg, DamageType.DAMAGE_TYPE_MAGICAL,
                DamageSource.DAMAGE_SOURCE_SPELLAOE, false);

            AddBuff("Slow", 2f, 1, qSpell, u, _owner);

            if (u is Champion)
                champHits++;
        }

        // Return missiles: each champion hit sends a heal back to Soraka
        for (int i = 0; i < champHits; i++)
        {
            SpellCast(_owner, 4, SpellSlotType.ExtraSlots, true, _owner, pos);
        }

        missile.SetToRemove();
    }

    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}

public class SorakaQReturnMissile : ISpellScript
{
    private ObjAIBase _owner;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    public void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        // Return missile reached Soraka — heal
        var qSpell = _owner.GetSpell("SorakaQ");
        int rank = qSpell.CastInfo.SpellLevel;

        float missingHpPct = 1f - _owner.Stats.CurrentHealth / _owner.Stats.HealthPoints.Total;
        float[] baseH = { 0, 25, 35, 45, 55, 65 };
        float[] maxH  = { 0, 50, 70, 90, 110, 130 };

        float heal = Math.Min(baseH[rank] * (1f + missingHpPct), maxH[rank]);
        _owner.TakeHeal(_owner, heal, HealType.SelfHeal);

        AddParticleTarget(_owner, _owner, "global_ss_heal_02.troy", _owner);

        missile.SetToRemove();
    }

    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}
