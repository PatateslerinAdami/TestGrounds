using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CharScripts;
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
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TalonNoxianDiplomacy : ISpellScript
{
    private ObjAIBase _talon;

    public static readonly string BuffName = "TalonNoxianDiplomacyBuff";

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _talon = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        AddBuff(BuffName, 5f, 1, spell, _talon, _talon);
        
        _talon.SetAutoAttackSpell("TalonNoxianDiplomacyAttack", true);
        
    }

    public void OnUpdateStats(AttackableUnit unit, float diff)
    {
        SetSpellToolTipVar(_talon, 0, _talon.Stats.AttackDamage.FlatBonus,
            SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);

        SetSpellToolTipVar(_talon, 1, _talon.Stats.AttackDamage.FlatBonus * 0.167f,
            SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}

public class TalonNoxianDiplomacyAttack : ISpellScript
{
    private ObjAIBase _talon;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new()
    {
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _talon = owner;
        _spell = spell;
        ApiEventManager.OnPreDealDamage.AddListener(this, owner, OnPreDealDamage);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        //_talon.PlayAnimation("Spell1");
    }

    public void OnPreDealDamage(DamageData data)
    {
        if (data.Attacker != _talon) return;
        if (!data.IsAutoAttack) return;
        if (!_talon.HasBuff(TalonNoxianDiplomacy.BuffName)) return;

        var qSpell = _talon.GetSpell("TalonNoxianDiplomacy");
        var level = qSpell?.CastInfo.SpellLevel ?? 1;

        var bonusDmg =
            30f + 30f * (level - 1) +
            _talon.Stats.AttackDamage.FlatBonus;

        // Add bonus damage BEFORE mitigation, then recompute
        data.Damage += bonusDmg;
        data.PostMitigationDamage = data.Target.Stats.GetPostMitigationDamage(
            data.Damage,
            DamageType.DAMAGE_TYPE_PHYSICAL,
            _talon
        );

        if (data.Target is Champion)
            AddBuff("TalonBleedBuff", 6f, 1, qSpell, data.Target, _talon);

        // Return to normal AA cycling and consume the buff
        _talon.ResetAutoAttackSpell();
        RemoveBuff(_talon, TalonNoxianDiplomacy.BuffName);
    }
}


