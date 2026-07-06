using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior;

namespace AIScripts
{
    // STUB — full implementation deferred (see memory: project_dominion_minion_ai_deferred).
    //
    // TempMinionAI (Scripts/TempMinionAI.lua, 4.20) is the autonomous owner-following temp-summon AI:
    // AI_PET_* states, GetGoldRedirectTarget() = owner (owner gone -> Die), teleport to owner when
    // >1500u (TELEPORT_DISTANCE), follow when >800u (FAR_MOVEMENT_DISTANCE), last-known-position
    // pathing when the owner is >1200u away (MINION_MAX_VISION_DISTANCE), targets via
    // FindTargetInAcRUsingGoldRedirectTarget; OnOrder only returns to owner (NOT player-commandable,
    // so distinct from the controllable Pet.cs / Pet.lua). Consumer in 4.20 not yet identified.
    //
    // Until built it is a no-op placeholder so the AIScript name resolves; a unit assigned it simply
    // idles rather than crashing.
    public class TempMinionAI : BaseAIScript
    {
        protected override void OnActivateBehavior()
        {
        }
    }
}
