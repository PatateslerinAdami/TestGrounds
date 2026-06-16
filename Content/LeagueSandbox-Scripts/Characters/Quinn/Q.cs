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
// HUMAN Q — QuinnQ: Skillshot blind (4.20: ExtraSpell3 = QuinnQMissile)
// ============================================================================
public class QuinnQ : ISpellScript
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
    public void OnSpellCast(Spell spell)
    {
        AddParticleTarget(_owner, _owner, "Quinn_Base_Q_Cas.troy", _owner, 1f);
    }
    public void OnSpellPostCast(Spell spell)
    {
        // Cast ExtraSlot 3 (QuinnQMissile) → client renders Quinn_Base_Q_Mis.troy
        SpellCast(_owner, 3, SpellSlotType.ExtraSlots, _targetPos, _targetPos, false, Vector2.Zero);

        // Direct damage + blind as fallback (runs immediately, missile visual still flies)
        int rank = spell.CastInfo.SpellLevel;
        float[] dmg = { 0, 70, 110, 150, 190, 230 };
        float bonusAd = _owner.Stats.AttackDamage.FlatBonus * 0.65f;
        float ap = _owner.Stats.AbilityPower.Total * 0.5f;

        // Line search from owner to target
        var dir = Vector2.Normalize(_targetPos - _owner.Position);
        float range = 1025f;
        var hitPos = _owner.Position + dir * range;
        AddParticle(_owner, null, "Quinn_Base_Q_Mis_Tar.troy", hitPos, 2f);

        var enemies = GetUnitsInRange(_owner, hitPos, 200f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var u in enemies)
        {
            if (u.Team == _owner.Team) continue;
            u.TakeDamage(_owner, dmg[rank] + bonusAd + ap, DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            AddBuff("Blind", 1.5f, 1, spell, u, _owner);
            AddParticleTarget(_owner, u, "Ezreal_essenceflux_tar.troy", u, 1f);
        }
    }
    public void OnUpdate(float diff) { }
}

// ============================================================================
// VALOR Q — QuinnValorQ: AoE slash (275 radius)
// 4.20: 70/110/150/190/230 + 0.65 bAD + 0.5 AP
// ============================================================================
public class QuinnValorQ : ISpellScript
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
    public void OnSpellCast(Spell spell)
    {
        AddParticleTarget(_owner, _owner, "FerosciousHowl_cas3.troy", _owner, 1f);
    }
    public void OnSpellPostCast(Spell spell)
    {
        int rank = spell.CastInfo.SpellLevel;
        float[] dmg = { 0, 70, 110, 150, 190, 230 };
        float bonusAd = _owner.Stats.AttackDamage.FlatBonus * 0.65f;
        float ap = _owner.Stats.AbilityPower.Total * 0.5f;

        var enemies = GetUnitsInRange(_owner, _owner.Position, 275f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var u in enemies)
        {
            if (u.Team == _owner.Team) continue;
            u.TakeDamage(_owner, dmg[rank] + bonusAd + ap, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            AddParticleTarget(_owner, u, "HuntersCall_eff2.troy", u, 1f);
        }
    }
    public void OnUpdate(float diff) { }
}

/// <summary>
/// QuinnQMissile — retained for ExtraSlot missile visual only.
/// Damage/blind handled directly by QuinnQ.OnSpellPostCast above.
/// </summary>
public class QuinnQMissile : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        MissileParameters = new MissileParameters { Type = MissileType.Circle },
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}
