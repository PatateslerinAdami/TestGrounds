using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using ShacoScripts;

namespace Buffs
{
    public class Backstab : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnPreDealDamage.AddListener(this, unit, OnDamage);
        }

        private void OnDamage(DamageData data)
        {
            bool directStrike = data.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK
                || data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELL;
            if (!directStrike || data.Damage <= 0 || data.Target is not (Champion or Minion)
                || !ShacoHelpers.IsBehind(data.Attacker, data.Target)) return;

            data.Damage *= 1.2f;
            data.PostMitigationDamage *= 1.2f;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnPreDealDamage.RemoveListener(this);
        }
    }
}
