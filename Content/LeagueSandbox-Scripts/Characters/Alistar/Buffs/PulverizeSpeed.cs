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
    internal class PulverizeSpeed : IBuffGameScript
    {
        private ObjAIBase _alistar;
        private AttackableUnit _target;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
        };

        public StatsModifier StatsModifier { get; private set; }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _alistar = ownerSpell.CastInfo.Owner;
            buff.Variables.Get("target", _target);
            ApiEventManager.OnMoveBegin.AddListener(this, _alistar, OnMoveBegin);
            //ForceMove(unit, _target.Position,10, 20, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, orders: ForceMovementOrdersType.CANCEL_ORDER);
        }

        private void OnMoveBegin(AttackableUnit unit, ForceMovementParameters parameters)
        {
            
        }
    }
}
