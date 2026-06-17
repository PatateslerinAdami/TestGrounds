namespace AIScripts
{
    // TaskCastSpell.lua is an EMPTY STUB: it sets a priority (MP/MaxMP * 0.48 when the bot has a target)
    // but BeginTask/Tick do NOTHING — the crude Lua bot never actually casts. Real per-champion spell use
    // lived in the behavior-tree (KillChampionAttackSequence etc.), not the Lua.
    //
    // DEVIATION (flagged): we add a MINIMAL GENERIC caster — fire the first ready+castable ability
    // (Q/W/E/R) at the bot's current target through the normal cast pipeline (Spell.Cast, which validates
    // mana/range/targeting and no-ops if invalid). Crude and not authentic: no per-champion logic
    // (skillshot aiming, combo order, ult-saving, self-buffs), it's MP-gated so manaless champions
    // (Garen/energy) never cast, and 0.48 loses to KillHero's 0.5 so it rarely wins priority — spell use
    // is sparse, faithful to the Lua task's weak design.
    public class TaskCastSpell : BotTask
    {
        public override void UpdatePriority(BotAI bot)
        {
            Priority = bot.HasEnemyTarget ? bot.ManaRatio * 0.48f : 0.0f;
        }

        public override void Tick(BotAI bot)
        {
            bot.TryCastReadyAbilityAtTarget();
        }
    }
}
