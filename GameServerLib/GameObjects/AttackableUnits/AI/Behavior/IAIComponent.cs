using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior
{
    /// <summary>
    /// A pluggable AI behavior module, mirroring Riot's Lua AIComponent system (AddComponent +
    /// AIComponentX scripts). Multiple components stack on one AI (e.g. a river crab = regen +
    /// fear + flee + taunt + wander + skittish + river-lock) and react to <see cref="AIEvent"/>s
    /// emitted by the host <see cref="BaseAIScript"/>. Composition over inheritance.
    ///
    /// Lifecycle: <see cref="OnAttach"/> (= ComponentInit, subscribe to events here) →
    /// <see cref="OnUpdate"/> each AI tick (the component's own timer logic) →
    /// <see cref="OnDetach"/> (= ComponentHalt) on death/despawn.
    /// </summary>
    public interface IAIComponent
    {
        void OnAttach(BaseAIScript ai, ObjAIBase owner);

        void OnUpdate(float diff)
        {
        }

        void OnDetach()
        {
        }
    }
}
