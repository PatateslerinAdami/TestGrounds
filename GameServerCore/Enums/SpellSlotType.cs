namespace GameServerCore.Enums
{
    /// <summary>
    /// Base indices of the spellbook slot layout.
    /// Verified against the S4 mac decomp: the spellbook has exactly 63
    /// slots (0-62; Spellbook.h mCurManaCost[63], SpellBookClient.h mSpellsClient[63]).
    /// Layout: 0-3 QWER, 4-5 summoner spells, 6-12 items (trinket = 12, replay: 28,572
    /// cooldown packets on slot 12), 13 recall, 14 temp item, 15-44 runes,
    /// 45-60 extra spells (NDAttack at 45 replay-verified), 61 use-spell,
    /// 62 passive (last spellbook slot).
    /// Basic attacks (64-81) live in a SEPARATE container, not the spellbook
    /// (obj_AI_Base::GetBasicAttack via BipedHelpers::IndexFromBasicAttackSlot);
    /// their slot ids verified against BasicAttackTypes (CharacterEnums.h):
    /// NORMAL_SLOT1-9 = 64-72, CRITICAL_SLOT1-9 = 73-81, MAX = 82.
    /// Fixed: our old layout had Use=62/Passive=63 (and a "RespawnSpellSlot"
    /// at 61 that does not exist at Riot) - all shifted by one against the client.
    /// </summary>
    public enum SpellSlotType
    {
        SpellSlots = 0,
        SummonerSpellSlots = 4,
        InventorySlots = 6,
        BluePillSlot = 13,
        TempItemSlot = 14,
        RuneSlots = 15,
        ExtraSlots = 45,
        /// <summary>
        /// The use-object spell (Riot UseableComponent default mUseCooldownSpellSlot = 61;
        /// UserComponent casts with castInfo.slot == 61). Thresh lantern, Dominion relics.
        /// </summary>
        UseSpellSlot = 61,
        /// <summary>
        /// The champion passive (Riot Spellbook::SetSpells loads
        /// characterRecord-&gt;passiveSpell into GetSpellDataInst(62)). Last spellbook slot.
        /// </summary>
        PassiveSpellSlot = 62,
        /// <summary>
        /// First normal basic-attack slot (64-72; separate container, not spellbook).
        /// </summary>
        BasicAttackNormalSlots = 64,
        /// <summary>
        /// First critical basic-attack slot (73-81; Riot BASICATTACK_CRITICAL_SLOT1-9 -
        /// definitely needed, crit attack variants are real slots).
        /// </summary>
        BasicAttackCriticalSlots = 73
    }
}
