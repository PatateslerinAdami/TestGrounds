using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    internal class RivenPassiveAABoost : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 3,
        };
        Buff thisBuff;
        private bool _isSubscribed = false;
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            thisBuff = buff;
            if (!_isSubscribed)
            {
                ApiEventManager.OnHitUnit.AddListener(this, ownerSpell.CastInfo.Owner, OnHitUnit, false);
                _isSubscribed = true;
            }
            var stackcount = thisBuff.StackCount;
        }
        public void OnHitUnit(DamageData damageData)
        {
            var target = damageData.Target;
            damageData.PostMitigationDamage += target.Stats.GetPostMitigationDamage(10f, damageData.DamageType, damageData.Attacker);
            thisBuff.ResetTimeElapsed();
            thisBuff.DecrementStackCount();
        }
    }
}
