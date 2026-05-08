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

        private ObjAIBase _owner;
        private Buff _buff;

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
                var itemSpell = GetItemSpell(3057);
                itemSpell?.SetCooldown(1.5f, true);

                RemoveBuff(_buff);

                if (damageAmount <= 0.0f)
                {
                    damageAmount = _owner.Stats.AttackDamage.BaseValue;
                }

                damageData.Target.TakeDamage(_owner, damageAmount, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
                return;
            }

            if (sourceItemId == 3078)
            {
                var itemSpell = GetItemSpell(3078);
                itemSpell?.SetCooldown(1.5f, true);

                RemoveBuff(_buff);

                if (damageAmount <= 0.0f)
                {
                    damageAmount = _owner.Stats.AttackDamage.BaseValue * 2f;
                }

                damageData.Target.TakeDamage(_owner, damageAmount, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
                return;
            }

            RemoveBuff(_buff);
        }

        private Spell GetItemSpell(int itemId)
        {
            for (byte i = 0; i < 7; i++)
            {
                var item = _owner.Inventory.GetItem(i);
                if (item != null && item.ItemData.ItemId == itemId)
                {
                    short spellSlot = (short)(i + (byte)SpellSlotType.InventorySlots);
                    if (_owner.Spells.TryGetValue(spellSlot, out Spell s))
                    {
                        return s;
                    }
                }
            }

            return null;
        }
    }
}
