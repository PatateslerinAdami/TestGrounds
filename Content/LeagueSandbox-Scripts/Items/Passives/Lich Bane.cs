using System.Linq;
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

namespace ItemPassives
{
    public class ItemID_3100 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();
        private ObjAIBase _owner = null!;
        private const int ItemId = 3100;
        private const float LichBaneScaling = 0.75f;
        private float _lastTooltipApDamage = -1.0f;
        private float _lastTooltipBaseAdDamage = -1.0f;

        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            SpellbladeManager.Register(owner, ItemId);

            Enumerable.Range(0, 4)
                .Where(slot => _owner.Spells.ContainsKey((short)slot))
                .Select(slot => _owner.Spells[(short)slot])
                .ToList()
                .ForEach(spell => ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellCast));

            ApiEventManager.OnUpdateStats.AddListener(this, owner, OnStatsUpdate, false);
            UpdateTooltipVars();
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            SpellbladeManager.Unregister(owner, ItemId);
            ApiEventManager.RemoveAllListenersForOwner(this);
            _owner = null!;
        }

        private void OnSpellCast(Spell spell)
        {
            SpellbladeManager.TryArmSpellblade(_owner, spell);
        }

        private void OnStatsUpdate(AttackableUnit unit, float diff)
        {
            UpdateTooltipVars();
        }

        private void UpdateTooltipVars()
        {
            if (_owner == null)
            {
                return;
            }

            var itemSlot = GetLichBaneInventorySlot();
            if (itemSlot < 0)
            {
                return;
            }

            var spellSlot = (short)(itemSlot + (byte)SpellSlotType.InventorySlots);
            if (!_owner.Spells.ContainsKey(spellSlot))
            {
                return;
            }

            var apDamage = _owner.Stats.AbilityPower.Total * LichBaneScaling;
            var baseAdDamage = _owner.Stats.AttackDamage.BaseValue * LichBaneScaling;

            if (_lastTooltipApDamage != apDamage)
            {
                _lastTooltipApDamage = apDamage;
                SetSpellToolTipVar(_owner, 0, apDamage, SpellbookType.SPELLBOOK_CHAMPION, (byte)itemSlot, SpellSlotType.InventorySlots);
            }

            if (_lastTooltipBaseAdDamage != baseAdDamage)
            {
                _lastTooltipBaseAdDamage = baseAdDamage;
                SetSpellToolTipVar(_owner, 1, baseAdDamage, SpellbookType.SPELLBOOK_CHAMPION, (byte)itemSlot, SpellSlotType.InventorySlots);
            }
        }

        private int GetLichBaneInventorySlot()
        {
            return Enumerable.Range(0, 7)
                .Where(slot => _owner.Inventory.GetItem((byte)slot)?.ItemData.ItemId == ItemId)
                .DefaultIfEmpty(-1)
                .First();
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
            if (damageData.IsAutoAttack && damageData.Target != null && !damageData.Target.IsDead)
            {
                var sourceItemId = _buff.Variables.GetInt("sourceItemId", 3100);
                var bonusDamage = _buff.Variables.GetFloat("damageAmount", 0.0f);

                if (bonusDamage <= 0.0f)
                {
                    float BonusBaseADValue = _owner.Stats.AttackDamage.BaseValue * 0.75f;
                    float BonusAPRatioDamage = _owner.Stats.AbilityPower.Total * 0.75f;
                    bonusDamage = BonusBaseADValue + BonusAPRatioDamage;
                }

                RemoveBuff(_buff);
                damageData.Target.TakeDamage(_owner, bonusDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
                AddBuff("SheenDelay", 1.5f, 1, _buff.OriginSpell, _owner, _owner);

                var LichBaneItemSpell = SpellbladeManager.GetItemSpell(_owner, sourceItemId);
                if (LichBaneItemSpell != null)
                {
                    LichBaneItemSpell.SetCooldown(1.5f, true);
                }
            }
        }
    }
}
