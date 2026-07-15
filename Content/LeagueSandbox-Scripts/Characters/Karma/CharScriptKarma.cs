using System.Linq;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptKarma : ICharScript {
    private const byte MantraSlot = 3;
    private ObjAIBase _owner;

    public void OnActivate(ObjAIBase owner, Spell spell = null) {
        _owner = owner;
        AddBuff("KarmaPassive", 25000f, 1, spell, _owner, _owner, true);

        // Mantra ranks up for FREE at the champion levels in Karma's chardata table
        // (SpellsUpLevels4 = {1,6,11,16}, MaxLevels R = 4) — rank 1 at spawn, then 6/11/16 via
        // OnLevelUp. There is no engine automatism for this (no data marker distinguishes
        // auto-ranking ults from skillable ones — Udyr's R shares the "starts at 1" table shape
        // but is player-skilled), so the auto-grant lives here in the char script, Riot-style.
        // GrantFreeMantraRanks reuses CanLevelUpSpell, so the grants follow the chardata table
        // instead of hardcoded levels — this also keeps the slot permanently non-skillable
        // (its rank always equals the level-gated max, so SpellSlotCanBeUpgraded yields 0).
        GrantFreeMantraRanks();
        ApiEventManager.OnLevelUp.AddListener(this, _owner, _ => GrantFreeMantraRanks(), false);
    }

    private void GrantFreeMantraRanks() {
        if (_owner is not Champion champion) {
            return;
        }

        var mantraSpell = champion.GetSpell("KarmaMantra");
        while (mantraSpell != null && champion.CanLevelUpSpell(mantraSpell)
               && champion.LevelUpSpell(MantraSlot, false) != null) {
        }
    }
}
