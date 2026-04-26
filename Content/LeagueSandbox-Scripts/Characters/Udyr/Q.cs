using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class UdyrTigerStance : ISpellScript {
    private ObjAIBase _udyr;
    private Spell     _spell;

    public StatsModifier StatsModifier { get; } = new();

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
        // TODO
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _spell = spell;
        _udyr = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _udyr, OnUpdateStats);
    }
    
    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var bonusDmg1 = _udyr.Stats.AttackDamage.Total * 0.15f;
        var bonusDmg2 = 30f + 50f                      * (_spell.CastInfo.SpellLevel - 1) +
                        _udyr.Stats.AttackDamage.Total * (1.2f + 0.1f * (_spell.CastInfo.SpellLevel - 1));
        SetSpellToolTipVar(_udyr, 0, bonusDmg1, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_udyr, 1, bonusDmg2, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
    
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        
        AddBuff("UdyrTigerStance",   25000f, 1, spell, owner,  owner, true);
        AddBuff("UdyrTigerPunch",   5f, 1, spell, owner,  owner);
    }
}

public class UdyrTigerAttack : ISpellScript {
    private ObjAIBase _udyr;
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _udyr = owner;
    }
}