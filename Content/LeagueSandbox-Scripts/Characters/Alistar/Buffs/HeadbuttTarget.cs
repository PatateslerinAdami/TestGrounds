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
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.KNOCKBACK,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
        };
        public StatsModifier StatsModifier { get; private set; }
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _alistar = buff.SourceUnit;
            var castOrigin = new Vector2(buff.BuffVars.GetFloat("castOriginX"), buff.BuffVars.GetFloat("castOriginY"));
            ForceMoveAway(unit, _alistar, 650f, 1500, 20, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.KEEP_CURRENT_FACING, ForceMovementOrdersType.CANCEL_ORDER, awayFrom: castOrigin);
        }
    }
}
