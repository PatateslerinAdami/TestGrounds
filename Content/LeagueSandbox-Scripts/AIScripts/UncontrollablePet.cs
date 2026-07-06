using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace AIScripts
{
    // Autonomous pet brain — faithful port of Riot's UncontrollablePet.lua (4.20): Malzahar Voidling
    // and similar. Reuses the controllable Pet brain (the two 0.15s timers, acquire/attack, leash,
    // arrival, CC) with the three differences UncontrollablePet.lua has:
    //   * ignores player orders (OnOrder is a no-op),
    //   * its "owner" is the GoldRedirectTarget — the summoner that kills credit (via the engine's
    //     Die() gold-redirect); falls back to the spawn owner when no redirect has been set,
    //   * actively walks to the owner once it drifts past FAR_MOVEMENT_DISTANCE (800), from any state,
    //     not only RETURN-from-idle (the >vision "blind move to last-known" branch collapses to the
    //     same path-to-owner because the server always knows the owner's position).
    // Flee is driven by the shared CrowdControlComponent (the buff raises the flag), exactly like Fear.
    public class UncontrollablePet : Pet
    {
        protected override ObjAIBase ResolveOwner()
            => (Owner.GoldRedirectTarget as ObjAIBase) ?? base.ResolveOwner();

        protected override bool ActivelyFollowsOwner => true;
        protected override float ActiveFollowDistance => 800.0f;

        // Not player-controllable — ignore all orders (UncontrollablePet.lua OnOrder just returns true).
        public override void OnOrder(OrderType order, AttackableUnit target, Vector2 pos) { }
    }
}
