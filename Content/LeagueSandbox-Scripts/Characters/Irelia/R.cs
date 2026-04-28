using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class IreliaTranscendentBlades : ISpellScript
{
    ObjAIBase _irelia;
    AttackableUnit _target;
    Spell _spell;
    private Vector2 _targetPos;
    bool _firstCast = true;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _irelia = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _irelia, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        if (_irelia.HasBuff("IreliaTranscendentBladesSpell"))
        {
            LogInfo("StackCount: " + _irelia.GetBuffWithName("IreliaTranscendentBladesSpell").StackCount);
        }
        else
        {
            PlayAnimation(_irelia, "Spell4", 1f);
            AddBuff("IreliaTranscendentBlades", 10.0f, 1, spell, _irelia, _irelia);
            AddBuff("IreliaTranscendentBladesSpell", 10.0f, 4, spell, _irelia, _irelia);
        }
    }

    public void OnSpellCast(Spell spell)
    {
        _targetPos = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
    }

    public void OnSpellPostCast(Spell spell)
    {
        SpellCast(_irelia, 0, SpellSlotType.ExtraSlots, _targetPos, _targetPos, true, _irelia.Position);
        spell.SetCooldown(0.5f, true);
        _irelia.GetBuffWithName("IreliaTranscendentBladesSpell").DecrementStackCount();
    }

    private void OnUpdateStats(AttackableUnit unit, float diff)
    {
        float ad = _irelia.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient2;
        float ap = _irelia.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        _spell.SetToolTipVar(0, ad);
        _spell.SetToolTipVar(1, ap);
    }
}

public class IreliaTranscendentBladesSpell : ISpellScript
{
    private ObjAIBase _irelia;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Circle
        },
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _irelia = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        if (target.Team == _irelia.Team || target is ObjBuilding) return;
        var mainSpell = _irelia.GetSpell("IreliaTranscendentBlades");
        
        float ad = _irelia.Stats.AttackDamage.FlatBonus * mainSpell.SpellData.Coefficient2;
        float ap = _irelia.Stats.AbilityPower.Total * mainSpell.SpellData.Coefficient;
        float dmg = 80f + 40 * (_irelia.GetSpell("IreliaTranscendentBlades").CastInfo.SpellLevel - 1) + ad + ap;

        AddParticleTarget(_irelia, target, "irelia_ult_tar", target);
        target.TakeDamage(_irelia, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
        float heal;
        
        if (IsValidTarget(_irelia, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes))
        {
            heal = target.Stats.GetPostMitigationDamage(dmg, DamageType.DAMAGE_TYPE_PHYSICAL, _irelia) * 0.25f;
            _irelia.TakeHeal(_irelia, heal, HealType.SelfHeal);
        }
        else if (IsValidTarget(_irelia, target,
                     SpellDataFlags.AffectEnemies | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral))
        {
            heal = target.Stats.GetPostMitigationDamage(dmg, DamageType.DAMAGE_TYPE_PHYSICAL, _irelia) * 0.1f;
            _irelia.TakeHeal(_irelia, heal, HealType.SelfHeal);
        }
    }
}