using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptSion : ICharScript {
    private ObjAIBase _sion;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _sion = owner;
        ApiEventManager.OnLevelUpSpell.AddListener(this, _sion.Spells[1], OnLevelUpSpell);
    }
    
    public void OnPostActivate(ObjAIBase owner, Spell spell = null)
    {
        AddBuff("SionPassive", 25000f, 1, spell, owner, owner, true);
    }

    private void OnLevelUpSpell(Spell spell)
    {
        if (spell.CastInfo.SpellLevel != 1) return;
        _sion.CharVars.Set("SoulFurnaceStacks", 0f);
        ApiEventManager.OnUpdateStats.AddListener(this, _sion, OnUpdateStats);
        ApiEventManager.OnKillUnit.AddListener(this, _sion, OnUnitKill);
        ApiEventManager.OnLevelUpSpell.RemoveListener(this, _sion.Spells[1], OnLevelUpSpell);
    }
    
    private void OnUnitKill(DeathData data)
    {
        // UnitTags is a [Flags] composite (a siege minion is Minion | Minion_Lane |
        // Minion_Lane_Siege), so equality patterns never match a live unit — query with
        // ContainsAny, specific tags first since those units also carry the Minion bit.
        var tags = data.Unit.UnitTags;
        if (tags.ContainsAny(UnitTag.Minion_Lane_Siege | UnitTag.Minion_Lane_Super | UnitTag.Champion
                             | UnitTag.Champion_Clone | UnitTag.Monster_Large | UnitTag.Monster_Epic))
        {
            _sion.Stats.HealthPoints.FlatBonus += 15f;
            _sion.CharVars.Set("SoulFurnaceStacks", _sion.CharVars.GetFloat("SoulFurnaceStacks") + 15f);
        }
        else if (tags.ContainsAny(UnitTag.Minion | UnitTag.Minion_Lane | UnitTag.Ward | UnitTag.Minion_Summon))
        {
            _sion.Stats.HealthPoints.FlatBonus += 4f;
            _sion.CharVars.Set("SoulFurnaceStacks", _sion.CharVars.GetFloat("SoulFurnaceStacks") + 4f);
        }
    }
    
    private void OnUpdateStats(AttackableUnit unit, float diff)
    {
        SetSpellToolTipVar(_sion, 0, _sion.CharVars.GetFloat("SoulFurnaceStacks"), SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
        var bonusMaxHealthDmg = _sion.Stats.HealthPoints.Total *
            (_sion.Spells[1].SpellData.EffectLevelAmount[6][_sion.Spells[1].CastInfo.SpellLevel]/100);
        SetSpellToolTipVar(_sion, 1, bonusMaxHealthDmg, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }
}