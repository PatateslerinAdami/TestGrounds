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
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

// ============================================================================
// HAMMER Q — JayceToTheSkies: Leap strike + AoE slow
// ============================================================================
public class JayceToTheSkies : ISpellScript
{
    private ObjAIBase _owner;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _owner = owner; }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell)
    {
        int rank = spell.CastInfo.SpellLevel;
        float[] dmg = { 0, 20, 65, 110, 155, 200 };
        float[] slow = { 0, 30, 35, 40, 45, 50 };

        var pos3 = spell.CastInfo.TargetPosition;
        var pos   = new Vector2(pos3.X, pos3.Z);
        _owner.DashToLocation(pos, 1000f, "Spell1", 0f, false, false);

        AddParticle(_owner, null, "Jayce_Base_Q_ToTheSkies_Tar.troy", pos, 1.5f);
        AddParticleTarget(_owner, _owner, "Jayce_Base_Q_ToTheSkies_Cas.troy", _owner, 1f);

        var enemies = GetUnitsInRange(_owner, pos, 275f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var u in enemies)
        {
            float extraAd = _owner.Stats.AttackDamage.FlatBonus * 1.0f;
            u.TakeDamage(_owner, dmg[rank] + extraAd, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            AddBuff("Slow", 2f, 1, spell, u, _owner);
            // Slow amount is handled primarily by the buff system; the spell data has Effect1 = slow%
        }
    }
    public void OnUpdate(float diff) { }
}

// ============================================================================
// CANNON Q — JayceShockBlast: Skillshot + gate-empowered variant
// ============================================================================
public class JayceShockBlast : ISpellScript
{
    private ObjAIBase _owner;
    private Vector2   _targetPos;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _owner = owner; }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _targetPos = new Vector2(end.X, end.Y);
    }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell)
    {
        // ExtraSpell1 = JayceShockBlastMis (line missile, CastType=3, MissileEffect=internal)
        // ExtraSpell2 = JayceShockBlastWallMis (gate-empowered, CastType=3)
        SpellCast(_owner, 1, SpellSlotType.ExtraSlots, _targetPos, _targetPos, true, Vector2.Zero);
    }
    public void OnUpdate(float diff) { }
}

/// <summary>
/// Shock Blast missile impact handler.
/// </summary>
public class JayceShockBlastMis : ISpellScript
{
    private ObjAIBase _owner;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        MissileParameters = new MissileParameters { Type = MissileType.Circle },
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnHit);
    }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { ApiEventManager.RemoveAllListenersForOwner(this); }

    public void OnHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        var pos = missile.Position;
        int rank = _owner.GetSpell("JayceShockBlast").CastInfo.SpellLevel;
        float ap  = _owner.Stats.AbilityPower.Total * 1.2f;
        float ad  = _owner.Stats.AttackDamage.FlatBonus * 1.2f;
        float[] dmg = { 0, 70, 120, 170, 220, 270 };

        AddParticle(_owner, null, "Jayce_Base_Q_ShockBlast_Tar.troy", pos, 2f);

        var enemies = GetUnitsInRange(_owner, pos, 210f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var u in enemies)
            u.TakeDamage(_owner, dmg[rank] + ad + ap, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);

        missile.SetToRemove();
    }

    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}
