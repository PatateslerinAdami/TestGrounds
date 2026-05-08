using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemPassives;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class Sheen : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner = null!;
        private Buff _buff = null!;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (unit is ObjAIBase ai)
            {
                _owner = ai;
                _buff = buff;

                ApiEventManager.OnHitUnit.AddListener(this, _owner, OnHitUnit);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }

        private void OnHitUnit(DamageData damageData)
        {
            if (!damageData.IsAutoAttack || damageData.Target == null || damageData.Target.IsDead)
            {
                return;
            }

            var sourceItemId = _buff.Variables.GetInt("sourceItemId", 0);
            var damageAmount = _buff.Variables.GetFloat("damageAmount", 0.0f);

            if (sourceItemId == 3057)
            {
                var itemSpell = SpellbladeManager.GetItemSpell(_owner, 3057);
                itemSpell?.SetCooldown(1.5f, true);

                RemoveBuff(_buff);

                if (damageAmount <= 0.0f)
                {
                    damageAmount = _owner.Stats.AttackDamage.BaseValue;
                }

                damageData.Target.TakeDamage(_owner, damageAmount, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
                AddBuff("SheenDelay", 1.5f, 1, _buff.OriginSpell, _owner, _owner);
                return;
            }

            if (sourceItemId == 3078)
            {
                var itemSpell = SpellbladeManager.GetItemSpell(_owner, 3078);
                itemSpell?.SetCooldown(1.5f, true);

                RemoveBuff(_buff);

                if (damageAmount <= 0.0f)
                {
                    damageAmount = _owner.Stats.AttackDamage.BaseValue * 2f;
                }

                damageData.Target.TakeDamage(_owner, damageAmount, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
                AddBuff("SheenDelay", 1.5f, 1, _buff.OriginSpell, _owner, _owner);
                return;
            }

            RemoveBuff(_buff);
        }
    }
}
