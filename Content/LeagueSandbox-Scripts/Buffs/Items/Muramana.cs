using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public class Muramana : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            ApiEventManager.OnHitUnit.AddListener(this, _owner, Hit);
            ApiEventManager.OnDealDamage.AddListener(this, _owner, SpellHit);
        }

        private void Hit(DamageData data)
        {
            if (ItemEffectHelpers.IsSuccessfulBasicAttack(_owner, data))
            {
                Shock(data);
            }
        }

        private void SpellHit(DamageData data)
        {
            if (data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELL && data.PostMitigationDamage > 0)
            {
                Shock(data);
            }
        }

        private void Shock(DamageData data)
        {
            if (data.Target.Team == _owner.Team || data.Target.IsDead || data.Target is not ObjAIBase
                || data.Target is BaseTurret
                || data.Target is Minion { IsWard: true })
            {
                return;
            }

            float mana = _owner.Stats.CurrentMana * 0.03f;
            _owner.Stats.CurrentMana -= mana;
            data.Target.TakeDamage(
                _owner,
                mana * 2f,
                DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource.DAMAGE_SOURCE_PROC,
                false
            );
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
