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

namespace ItemPassives
{
    public class ItemID_3100 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();
        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;

            for (short i = 0; i <= 3; i++)
            {
                if (_owner.Spells.TryGetValue(i, out Spell spell) && spell != null)
                {
                    ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellCast);
                }
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }

        private void OnSpellCast(Spell spell)
        {
            Spell LichBaneItemSpell = GetLichBaneSpell();

            if (LichBaneItemSpell != null && LichBaneItemSpell.CurrentCooldown <= 0f && !_owner.HasBuff("LichBane"))
            {
                AddBuff("LichBane", 10.0f, 1, spell, _owner, _owner);
            }
        }

        private Spell GetLichBaneSpell()
        {
            for (byte i = 0; i < 7; i++)
            {
                var item = _owner.Inventory.GetItem(i);
                if (item != null && item.ItemData.ItemId == 3100)
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
namespace Buffs
{
    public class LichBane : IBuffGameScript
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
                float BonusBaseADValue = _owner.Stats.AttackDamage.BaseValue * 0.75f;
                float BonusAPRatioDamage = _owner.Stats.AbilityPower.Total * 0.75f;
                float BonusDamage = BonusBaseADValue + BonusAPRatioDamage;

                RemoveBuff(_buff);
                damageData.Target.TakeDamage(_owner, BonusDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, false);

                Spell LichBaneItemSpell = GetLichBaneSpell();
                if (LichBaneItemSpell != null)
                {
                    LichBaneItemSpell.SetCooldown(1.5f, true);
                }
            }
        }

        private Spell GetLichBaneSpell()
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
    }
}
