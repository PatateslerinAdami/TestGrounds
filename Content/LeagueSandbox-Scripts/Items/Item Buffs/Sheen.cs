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
    public class Sheen : IBuffGameScript // reason this had to be added in a dedicated script folder is because this buff is used both for sheen and trinity force, but they use different dmg values
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
            if (damageData.IsAutoAttack && damageData.Target != null && !damageData.Target.IsDead)
            {
                float SheenBonusDamage = _owner.Stats.AttackDamage.BaseValue;
                float TrinityBonusDamage = _owner.Stats.AttackDamage.BaseValue * 2f;

                Spell sheenItemSpell = GetSheenSpell();
                Spell TrinitySheenItemSpell = GetTrinitySheenSpell();

                if (sheenItemSpell != null)
                {
                    sheenItemSpell.SetCooldown(1.5f, true);
                    damageData.Target.TakeDamage(_owner, SheenBonusDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false);
                }
                else if (TrinitySheenItemSpell != null)
                {
                    TrinitySheenItemSpell.SetCooldown(1.5f, true);
                    damageData.Target.TakeDamage(_owner, TrinityBonusDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false);
                }
                RemoveBuff(_buff);
            }
        }

        private Spell GetSheenSpell()
        {
            for (byte i = 0; i < 7; i++)
            {
                var item = _owner.Inventory.GetItem(i);
                if (item != null && item.ItemData.ItemId == 3057)
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

        private Spell GetTrinitySheenSpell()
        {
            for (byte i = 0; i < 7; i++)
            {
                var item = _owner.Inventory.GetItem(i);
                if (item != null && item.ItemData.ItemId == 3078)
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