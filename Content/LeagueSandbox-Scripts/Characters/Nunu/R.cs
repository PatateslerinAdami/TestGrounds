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

public class AbsoluteZero : ISpellScript {
    private ObjAIBase _nunu;
    private AttackableUnit _target;
    private Particle _particle;
    private Spell _spell;
    private int _wallId;
    // Elapsed channel time in seconds (mirrors the S1 CharVars.LifeTime) — drives the damage ramp.
    private float _channelElapsed;

    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
        IsDamagingSpell = true,
        ChannelDuration = 3
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nunu = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        _spell = spell;
    }

    public void OnSpellChannel(Spell spell)
    {
        _channelElapsed = 0f;
        AddBuff("AbsoluteZero", 3f, 1, spell, _nunu, _nunu);
        _particle = SpellEffectCreate("AbsoluteZero2_green_cas.troy", _nunu, _nunu,
            effectNameForEnemy: "AbsoluteZero2_red_cas.troy", fowVisibilityRadius: 10, lifetime: 3f);
        var units = GetUnitsInRange(_nunu, _nunu.Position, 575f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectHeroes);

        foreach (var unit in units)
        {
            AddBuff("AbsoluteZeroSlow", 3f, 1, _spell, unit, _nunu);
        }
        //_wallId = CreateAreaTriggerSphere(_nunu.Position, 575f, OnEnterArea, OnExitArea);
    }

    /*private void OnEnterArea(AttackableUnit unit)
    {
        if (IsValidTarget(_nunu, unit,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectHeroes))
        {
            AddBuff("AbsoluteZeroSlow", 3f, 1, _spell, unit, _nunu);
        }
    }
    
    private void OnExitArea(AttackableUnit unit)
    {
        if (unit.HasBuff("AbsoluteZeroSlow"))
        {
            RemoveBuff(_target, "AbsoluteZeroSlow", _nunu);
        }
    }*/

    public void OnSpellChannelUpdate(Spell spell, float diff)
    {
        _channelElapsed += diff / 1000f;   // diff is in ms — accumulate seconds channelled
    }

    // Fully channelled -> 100% of the damage.
    public void OnSpellPostChannel(Spell spell)
    {
        RemoveBuff(_target, "AbsoluteZero", _nunu);
        RemoveParticle(_particle);
        DeleteAreaTrigger(_wallId);
        Detonate(1f);
    }

    // Interrupted early -> damage scaled by how long it was channelled (channelled / total).
    public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
    {
        RemoveBuff(_target, "AbsoluteZero", _nunu);
        RemoveParticle(_particle);
        DeleteAreaTrigger(_wallId);
        var channelTime = _spell.SpellData.EffectLevelAmount[4][_spell.CastInfo.SpellLevel]; // Effect4 = 3s
        var scale = channelTime > 0f ? System.Math.Clamp(_channelElapsed / channelTime, 0f, 1f) : 0f;
        Detonate(scale);
    }

    // Faithful to AbsoluteZero.json: Effect1 = base damage by level {625,875,1125}, Coefficient = 2.5 AP ratio.
    // Total = (Effect1 + 2.5*AP), applied * scale (1 on full channel, channelled/Effect4 on early cancel).
    private void Detonate(float scale)
    {
        var lvl = _spell.CastInfo.SpellLevel;
        var ap = _nunu.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var dmg = (_spell.SpellData.EffectLevelAmount[1][lvl] + ap) * scale;

        SpellEffectCreate("AbsoluteZero_nova.troy", _nunu, _nunu, fowVisibilityRadius: 10);
        var units = EnumerateValidUnitsInRange(_nunu, _nunu.Position, 650f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var unit in units)
        {
            SpellEffectCreate("AbsoluteZero_tar.troy", _nunu, unit, fowVisibilityRadius: 10);
            unit.TakeDamage(_nunu, dmg, DamageType.DAMAGE_TYPE_MAGICAL,
                DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
        }
    }
}

public class AbsoluteZero2 : ISpellScript
{
    private ObjAIBase _nunu;


    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = true,
        DoesntBreakShields = false,
        TriggersSpellCasts = false,
        CastingBreaksStealth = false,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _nunu = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        var mainSpell = _nunu.GetSpell("AbsoluteZero");
        SpellEffectCreate("AbsoluteZero_nova.troy", _nunu, _nunu, fowVisibilityRadius: 10);
        var units = EnumerateValidUnitsInRange(_nunu, _nunu.Position, 650f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var unit in units)
        {
            SpellEffectCreate("AbsoluteZero_tar.troy", _nunu, unit, fowVisibilityRadius: 10);
            
        }
    }
}