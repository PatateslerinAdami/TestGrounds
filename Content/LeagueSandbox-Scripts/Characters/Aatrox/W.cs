using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AatroxW : ISpellScript {
    private ObjAIBase _aatrox;
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _aatrox = owner;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        ApiEventManager.OnUpdateStats.AddListener(this, _aatrox, OnUpdateStats);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var adHeal = _aatrox.Stats.AttackDamage.FlatBonus * _aatrox.Spells[1].SpellData.Coefficient;
        var heal   = 20f + 5f * (_aatrox.Spells[1].CastInfo.SpellLevel - 1) + adHeal;
        heal *= 2f;
        SetSpellToolTipVar(_aatrox, 4, heal, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
        var ad   = _aatrox.Stats.AttackDamage.FlatBonus * _aatrox.Spells[1].SpellData.Coefficient;
        var cost = 15f + 8.75f * (_aatrox.Spells[1].CastInfo.SpellLevel - 1) + ad;
        SetSpellToolTipVar(_aatrox, 3, cost, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _aatrox.SetSpell("AatroxW2", 1, true);
        AddBuff("AatroxWPower", 25000f, 1, spell, _aatrox, _aatrox, true);
    }
    
    private void OnLevelUpSpell(Spell spell) {
        if (spell.CastInfo.SpellLevel != 1) return;
        AddBuff("AatroxWONHBuff", 25000f, 1, spell, _aatrox, _aatrox, true);
        AddBuff("AatroxWLife", 25000f, 1, spell, _aatrox, _aatrox, true);
    }
}

public class AatroxW2 : ISpellScript {
    private ObjAIBase _aatrox;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _aatrox = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _aatrox, OnUpdateStats);
    }
    
    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var adHeal = _aatrox.Stats.AttackDamage.BaseBonus * _aatrox.Spells[1].SpellData.Coefficient;
        var heal   = 20f + 5f * (_aatrox.Spells[1].CastInfo.SpellLevel - 1) + adHeal;
        heal *= 2f;
        SetSpellToolTipVar(_aatrox, 4, heal, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
        var ad   = _aatrox.Stats.AttackDamage.FlatBonus * _aatrox.Spells[1].SpellData.Coefficient;
        var cost = 15f + 8.75f * (_aatrox.Spells[1].CastInfo.SpellLevel - 1) + ad;
        SetSpellToolTipVar(_aatrox, 3, cost, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
        
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _aatrox.SetSpell("AatroxW", 1, true);
        AddBuff("AatroxWLife", 25000f, 1, spell, _aatrox, _aatrox, true);
    }
}

public class AatroxWHeal : ISpellScript {
    private ObjAIBase _aatrox;
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target,
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _aatrox = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var ad = _aatrox.Stats.AttackDamage.BaseBonus * _aatrox.Spells[1].SpellData.Coefficient;
        var heal   = 20f + 5f * (_aatrox.Spells[1].CastInfo.SpellLevel - 1) + ad;
        if (_aatrox.Stats.CurrentHealth <= _aatrox.Stats.HealthPoints.Total * 0.5f) {
            heal *= 2f;
        }

        var healParticle = _aatrox.SkinID switch {
            1 => "Aatrox_Skin01_W_Life_Self",
            2 => "Aatrox_Skin02_W_Life_Self",
            _ => "Aatrox_Base_W_Life_Self"
        };
        AddParticleTarget(_aatrox, _aatrox, healParticle, _aatrox);
        _aatrox.TakeHeal(_aatrox, heal, HealType.SelfHeal);
    }
}

public class AatroxWONHAttackLife : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell = true
    };
}

public class AatroxWONHAttackPower : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell = true
    };
}