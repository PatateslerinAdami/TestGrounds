using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameMaths;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs
{
    internal class HeadbuttTarget : IBuffGameScript
    {

        private ObjAIBase _alistar;
        private Spell _spell;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.KNOCKBACK,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = false
        };
        public StatsModifier StatsModifier { get; private set; }
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _alistar = ownerSpell.CastInfo.Owner;
            _spell = ownerSpell;
            // castOrigin is only used for the knockback DIRECTION (away-from anchor); after the charge
            // Alistar stands on the target, so his live position would be degenerate. The DISTANCE is a
            // fixed 650 in 4.20 (replay-verified: charge-distance-independent), speed 1500, gravity 20.
            // FURTHEST_WITHIN_RANGE trims it to the last walkable cell at walls, exactly like the replay.
            var castOrigin = new Vector2(buff.Variables.GetFloat("castOriginX"), buff.Variables.GetFloat("castOriginY"));
            ForceMoveAway(unit, _alistar, 650f, 1500, 20, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.KEEP_CURRENT_FACING, ForceMovementOrdersType.CANCEL_ORDER, awayFrom: castOrigin);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }
    }
}
