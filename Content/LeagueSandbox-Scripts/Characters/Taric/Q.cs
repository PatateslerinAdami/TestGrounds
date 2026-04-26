using System;
using System.Numerics;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class Imbue : ISpellScript {
    private ObjAIBase _taric;
    private AttackableUnit _target;
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _taric = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _taric, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
    }

    public void OnSpellPostCast(Spell spell) {
        var heal = 60f + _taric.Stats.AbilityPower.Total * 0.3f + _taric.Stats.HealthPoints.FlatBonus * 0.05f;
        _taric.TakeHeal(_taric, _taric == _target ? heal + heal * 0.4f : heal, HealType.SelfHeal);

        if (_taric == _target) return;
        AddParticleTarget(_taric, _target, "Global_Heal.troy", _target);
        _target.TakeHeal(_taric, heal, HealType.SelfHeal);
    }
    
    public void OnUpdateStats(AttackableUnit target, float diff) {
        SetSpellToolTipVar(_taric, 0, _taric.Stats.HealthPoints.FlatBonus * 0.05f, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_taric, 1, _taric.Stats.HealthPoints.FlatBonus * 0.05f + (_taric.Stats.HealthPoints.FlatBonus * 0.05f * 0.4f), SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}