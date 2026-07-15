using GameServerCore.Enums;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects
{
    /// <summary>
    /// Shop world object (Riot: <c>obj_Shop</c>) — the shopkeeper entity near the fountain. A distinct
    /// GameObject type so it can be tracked in its own registry (<c>ObjectManager._shops</c>), mirroring
    /// Riot's <c>obj_Shop</c> ManagerTemplate.
    ///
    /// <para>Deliberately derives from <see cref="GameObject"/>, NOT from a building/AttackableUnit base:
    /// Riot's <c>obj_Shop</c> inherits <c>obj_Building</c> for structure plumbing but is never a valid
    /// attack target (all its <c>IsBetterTargetThan*</c> return "not better"). Our AttackableUnit-derived
    /// buildings ARE targetable, so making a shop one would wrongly give it HP and let units attack it.
    /// A bare GameObject already can't be targeted — the behaviour we want — so we keep it minimal, like
    /// <see cref="Marker"/> / <see cref="LevelProp"/>.</para>
    ///
    /// <para>Named <c>ShopObject</c> (not <c>Shop</c>) to avoid colliding with the unrelated
    /// <c>Inventory.Shop</c>, which is a champion's shop-inventory/purchase logic, not a world object.</para>
    /// </summary>
    public class ShopObject : GameObject
    {
        // Radii default to GameObject's defaults (collision 40, vision 0) — identical to the previous
        // bare-GameObject shop created by APIMapFunctionManager.CreateShop, so behaviour is unchanged.
        // A shop is a static world object with no per-tick logic (no Update override) — opt out of
        // ObjectManager's update pass, mirroring Riot's obj-flag 0x100. Still networked/visioned.
        public override bool ReceivesUpdate => false;

        public ShopObject(Game game, Vector2 position, uint netId = 0, TeamId team = TeamId.TEAM_NEUTRAL)
            : base(game, position, netId: netId, team: team)
        {
        }
    }
}
