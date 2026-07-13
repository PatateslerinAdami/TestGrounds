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

public class WujuStyle : ISpellScript
{
    private ObjAIBase _masterYi;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = true,
        TriggersSpellCasts = true,
        ChannelDuration = 4,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _masterYi = owner;
        _spell = spell;
        //ApiEventManager.OnUpdateStats.AddListener(this, _masterYi, OnUpdateStats);
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        ApiEventManager.OnSpellCooldownEnd.AddListener(this, spell, OnSpellCooldownEnd);
    }
    
    private void OnLevelUpSpell(Spell spell) {
        if (_masterYi.HasBuff("WujuStyleSuperChargedVisual")) return;
        AddBuff("WujuStyleVisual", 25000f, 1, _spell, _masterYi, _masterYi, true);
    }

    public void OnSpellCast(Spell spell)
    {
        AddBuff("WujuStyleSuperChargedVisual", 4f, 1, spell, _masterYi, _masterYi);
    }
    
    private void OnSpellCooldownEnd(Spell spell) {
        AddBuff("WujuStyleVisual", 25000f, 1, _spell, _masterYi, _masterYi, true);
    }
    
    private void OnUpdateStats(AttackableUnit owner, float diff) {
        var bonusAd = _masterYi.Stats.AttackDamage.Total * _spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel]/100;
        var ad = _masterYi.Stats.AttackDamage.Total * 0.1f + 0.025f * (_spell.CastInfo.SpellLevel - 1);
        SetSpellToolTipVar(_masterYi, 0, bonusAd, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_masterYi, 1, ad, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
    }

}