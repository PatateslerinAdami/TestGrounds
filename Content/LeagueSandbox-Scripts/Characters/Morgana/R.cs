using System;
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
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SoulShackles : ISpellScript
{
    private ObjAIBase _morgana;
    private Spell _spell;
    private Particle _indicatorRing;
    private const float TetherRange = 630f;
    private bool _isCast = false;

    private readonly List<AttackableUnit> _enemiesTethered = [];

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _morgana = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        
        var ap = _morgana.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var dmg = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;
        if (_isCast)
        {

            var particleName = _morgana.SkinID switch
            {
                4 => "Morgana_Blackthorn_SoulShackle_tar_explode.troy",
                5 => "Morgana_Skin05_R_Tar_Explode.troy",
                6 => "Morgana_Skin06_R_Tar_Explode.troy",
                _ => "Morgana_Base_R_Tar_Explode.troy"
            };
            SpellEffectCreate(particleName, _morgana, target, null,
                flags: FXFlags.SimulateWhileOffScreen);
            AddBuff("Stun", 1.5f, 1, spell, target, _morgana);
        }
        else
        {
            _enemiesTethered.Add(target);
            AddBuff("SoulShackles", 3f, 1, spell, target, _morgana);
            var variables = new VariableTable();
            variables.Set("slowPercent", 0.2f);
            AddBuff("Slow", 3f, 1, spell, target, _morgana, variableTable: variables);
        }
        target.TakeDamage(_morgana, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        _isCast = false;
    }

    public void OnSpellPostCast(Spell spell)
    {
        var targets = GetUnitsHitBySpell(spell);
        foreach (var target in targets)
        {
            spell.ApplyEffects(target);
        }
        _indicatorRing = SpellEffectCreate("Morgana_base_R_Indicator_Ring.troy", _morgana, _morgana, _morgana,
            lifetime: 5f, flags: FXFlags.SimulateWhileOffScreen);
        _isCast = true;
    }

    public void OnUpdate(float diff)
    {
        SealSpellSlot(_morgana, SpellSlotType.SpellSlots, 3, SpellbookType.SPELLBOOK_CHAMPION,
            GetUnitsInRange(_morgana, _morgana.Position, TetherRange, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes).Count == 0);
        if (!_isCast) return;
        ExecutePeriodically(_spell.CastInfo.InstanceVars, "LastTimeExecuted", 500f, true, 6, () =>
        {
            foreach (var target in _enemiesTethered.ToList().Where(target => Vector2.Distance(_morgana.Position, target.Position) > TetherRange || target.IsDead))
            {
                RemoveBuff(target, "SoulShackles", _morgana);
                RemoveBuff(target, "Slow", _morgana);
                _enemiesTethered.Remove(target);
            }

            if (_enemiesTethered.Count != 0) return;
            RemoveParticle(_indicatorRing);
        });

        ExecutePeriodically(_spell.CastInfo.InstanceVars, "TimerMs", 3000f, false, 1, () =>
        {
            foreach (var target in _enemiesTethered.Where(target => !target.IsDead))
            {
                _spell.ApplyEffects(target);
            }

            RemoveParticle(_indicatorRing);
        });
    }
}