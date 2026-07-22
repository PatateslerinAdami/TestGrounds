namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    /// <summary>
    /// Port of Riot's <c>AIManager_Common</c> (<c>Game/LoL/AI/Object/Manager/AIManager.h</c>). In Riot it is
    /// owned by <c>obj_AI_Base</c> through <c>std::shared_ptr&lt;AIManager_Common&gt; PTR_AIManager</c> (offset
    /// 0x4cfc, accessor <c>GetAIManager()</c>) and holds the movement/collision engine by value as
    /// <c>Actor_Common AI_actor</c> (offset 0x4c). Here it hangs off <see cref="ObjAIBase.AIManager"/> and
    /// holds the <see cref="Actor"/> by composition.
    ///
    /// <para><b>STAGE 0 (scaffold).</b> A near-empty shell that owns the <see cref="Actor"/>. Riot's other
    /// AIManager duties (order routing, target tracking, etc.) are intentionally out of scope — do not fold
    /// unrelated logic in here. See <c>docs/ACTOR_CLASS_EXTRACTION_PLAN.md</c>.</para>
    /// </summary>
    public class AIManager
    {
        /// <summary>The unit that owns this manager (Riot: the <c>obj_AI_Base</c> holding
        /// <c>PTR_AIManager</c>).</summary>
        public ObjAIBase Owner { get; }

        /// <summary>The composed movement/collision engine (Riot: <c>AI_actor</c>).</summary>
        public Actor Actor { get; }

        public AIManager(ObjAIBase owner)
        {
            Owner = owner;
            Actor = new Actor(this);
        }
    }
}
