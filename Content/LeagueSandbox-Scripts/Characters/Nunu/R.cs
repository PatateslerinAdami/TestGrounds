using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AbsoluteZero : ISpellScript
{
    private ObjAIBase _nunu;
    private AttackableUnit _target;
    private Spell _spell;
    private float _scale = 0f;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
        IsDamagingSpell = true,
        ChannelDuration = 3
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _nunu = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _nunu, OnUpdateStats);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff)
    {
        var ap = _nunu.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var fullDmg = (_spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] + ap) * 0.125f;
        SetSpellToolTipVar(_nunu, 1, fullDmg, SpellbookType.SPELLBOOK_CHAMPION, 3, SpellSlotType.SpellSlots);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var ap = _nunu.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var dmg = (_spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] + ap) * _scale;
        SpellEffectCreate("AbsoluteZero_tar.troy", _nunu, target, target, flags: FXFlags.SimulateWhileOffScreen,
            fowVisibilityRadius: 0f);
        target.TakeDamage(_nunu, dmg, DamageType.DAMAGE_TYPE_MAGICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        _spell = spell;
    }

    public void OnSpellChannel(Spell spell)
    {
        _nunu.CharVars.Set("LifeTime", 0f);
        AddBuff("AbsoluteZero", 3f, 1, spell, _nunu, _nunu);

        var units = GetUnitsInRange(_nunu, _nunu.Position, 575f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectHeroes);

        foreach (var unit in units)
        {
            AddBuff("AbsoluteZeroSlow", 3f, 1, _spell, unit, _nunu);
        }
    }

    public void OnSpellChannelUpdate(Spell spell, float diff)
    {
        var lifeTime = _nunu.CharVars.GetFloat("LifeTime");
        lifeTime += diff / 1000f;
        _nunu.CharVars.Set("LifeTime", lifeTime);
    }

    public void OnSpellPostChannel(Spell spell)
    {
        RemoveBuff(_target, "AbsoluteZero", _nunu);
        _scale = 1f;
        Detonate();
    }

    public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
    {
        RemoveBuff(_target, "AbsoluteZero", _nunu);
        var channelTime = _spell.SpellData.EffectLevelAmount[4][_spell.CastInfo.SpellLevel]; // = 3
        var frac = channelTime > 0f
            ? System.Math.Clamp(_nunu.CharVars.GetFloat("LifeTime") / channelTime, 0f, 1f)
            : 0f;
        _scale = 0.125f + 0.75f * frac;
        Detonate();
    }

    private void Detonate()
    {
        SpellEffectCreate("AbsoluteZero_nova.troy", _nunu, null, _nunu, flags: FXFlags.SimulateWhileOffScreen,
            fowVisibilityRadius: 10);
        var units = EnumerateValidUnitsInRange(_nunu, _nunu.Position, 650f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var unit in units)
        {
            _spell.ApplyEffects(unit);
        }
    }
}

public class AbsoluteZero2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
    };
}