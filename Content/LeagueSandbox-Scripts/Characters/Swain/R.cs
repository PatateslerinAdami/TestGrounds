using System;
using System.Buffers;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SwainMetamorphism : ISpellScript
{
    private ObjAIBase _swain;
    private Spell _spell;
    private bool _isActive = false;
    private PeriodicTicker _periodicTicker;
    private float _manaCost;
    private float _tickIncrease;
    private bool _isFirstCast;


    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _swain = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _manaCost = 25;
        _tickIncrease = 5f + (spell.CastInfo.SpellLevel - 1);
        _spell = spell;
    }

    public void OnSpellCast(Spell spell)
    {
        switch (_swain.Model)
        {
            case "Swain":
            case "SwainNoBird":
                AddBuff("SwainMetaHealTracker", 1000000000f, 1, spell, _swain, _swain);
                _swain.ChangeModel("SwainRaven");
                AddParticleTarget(_swain, _swain, "swain_metamorph", _swain);
                AddParticleTarget(_swain, _swain, "swain_metamorph_02.troy", _swain);
                AddParticleTarget(_swain, _swain, "swain_demonForm_idle.troy", _swain);
                _isFirstCast = true;
                _isActive = true;
                break;
            case "SwainRaven": DisableMetamorphism(); break;
        }
    }

    public void OnSpellPostCast(Spell spell)
    {
        if (!_isActive) return;
        if (!(_swain.Stats.CurrentMana >= 25f + _tickIncrease)) return;
        spell.SetCooldown(0.3f, true);
        AddBuff("SwainMetaToggle", 0.3f, 1, spell, _swain, _swain);
    }

    public void OnUpdate(float diff)
    {
        if (!_isActive) return;
        var ticks = _periodicTicker.ConsumeTicks(diff, 1000f, true, 1);
        if (ticks != 1) return;
        ApplyBirds();
    }

    private void ApplyBirds()
    {
        var closestUnits = GetUnitsInRange(_swain, _swain.Position, 700f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes |
                SpellDataFlags.AffectMinions)
            .OrderBy(unit => unit is Champion).ThenBy(unit => unit.HasBuff("SwainTorment"))
            .ThenBy(unit => unit is Minion)
            .ThenBy(unit => Vector2.DistanceSquared(_swain.Position, unit.Position)).ToArray().Take(3);
        foreach (var unit in closestUnits)
        {
            //check if target is visible only when visible send birds
            SpellCast(_swain, 0, SpellSlotType.ExtraSlots, true, unit, _swain.Position);
        }

        if (!_isFirstCast)
        {
            _swain.Stats.CurrentMana -= _manaCost;
        }
        else
        {
            _isFirstCast = false;
        }

        _manaCost += _tickIncrease;
        if (_swain.Stats.CurrentMana < _manaCost)
        {
            DisableMetamorphism();
        }
    }

    private void DisableMetamorphism()
    {
        _isActive = false;
        var buff = _swain.GetBuffWithName("SwainMetaHealTracker").BuffScript as SwainMetaHealTracker;
        buff?.RequestDisable();
        _spell.SetSpellToggle(false);
        _swain.ChangeModel(_swain.HasBuff("SwainHasBeatrix") ? "SwainNoBird" : "Swain");
        AddParticleTarget(_swain, _swain, "swain_metamorph", _swain);
    }
}

public class SwainMetaNuke : ISpellScript
{
    private ObjAIBase _swain;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _swain = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        SpellCast(_swain, target.HasBuff("SwainTorment") ? 2 : 1, SpellSlotType.ExtraSlots, true, _swain,
            target.Position);
    }
}

public class SwainMetaHeal : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        },
    };
}

public class SwainMetaHealTorment : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        },
    };
}