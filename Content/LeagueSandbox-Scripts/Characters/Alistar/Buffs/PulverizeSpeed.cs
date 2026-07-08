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
        private Spell _spell;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.KNOCKUP,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsNonDispellable = false
        };
        public StatsModifier StatsModifier { get; private set; }
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _alistar = buff.SourceUnit;
            _spell = ownerSpell;
            var bouncePos = GetRandomPointInAreaUnit(unit, 10, 10f);
            ApiEventManager.OnMoveEnd.AddListener(this, unit, OnMoveEnd);
            ForceMove(unit, bouncePos,10f, 20f, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, orders: ForceMovementOrdersType.CANCEL_ORDER, idealDistance: 10f, movementName: "pulverize");
        }

        private void OnMoveEnd(AttackableUnit unit, ForceMovementParameters parameters)
        {
            if (parameters.MovementName != "pulverize") return;
            var ap = _alistar.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
            var dmg = _spell.SpellData.EffectLevelAmount[2][_alistar.Stats.Level] + ap;
            unit.TakeDamage(_alistar, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
            AddBuff("Pulverize", 0.5f, 1, _spell, unit, _alistar);
        }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
