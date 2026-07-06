namespace AIScripts
{
    // Base for a single bot utility task — faithful to Riot's Task*.lua objects (Bot.lua scheduler).
    // Each task scores itself (UpdatePriority), runs while it's the highest-priority task (BeginTask on
    // activation, Tick while active), and removes itself when Done. The BotAI script is passed in as the
    // "bot context" (mirrors the Lua tasks' implicit `me` + the global GetPos/GetRegroupPos/… helpers),
    // because BaseAIScript's movement/targeting helpers are protected — BotAI re-exposes what tasks need.
    public abstract class BotTask
    {
        // Display/identity name (Bot.lua sets per-instance, e.g. "Push Lane 0", "Retreat"). Used by the
        // scheduler debug + PushLane anti-stacking (GetActiveTaskName) later.
        public string Name = "";

        // Utility score set by UpdatePriority each scheduler tick (~0..1). Highest wins.
        public float Priority;

        // When true the scheduler drops this task (one-shot tasks; most bot tasks are persistent → false).
        public bool Done;

        public abstract void UpdatePriority(BotAI bot);
        public virtual void BeginTask(BotAI bot) { }
        public virtual void Tick(BotAI bot) { }
        public virtual void OnTargetLost(BotAI bot) { }
        public virtual void AntiKiteTimer(BotAI bot) { }
    }
}
