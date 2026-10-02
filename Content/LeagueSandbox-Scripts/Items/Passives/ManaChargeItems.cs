using System;
using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Inventory;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public abstract class ManaChargeItem : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();
        protected abstract int ItemId { get; }

        private ObjAIBase _owner;
        private float _lastMana;
        private bool _castSpend;
        private bool IsCharged => ItemId == 3040 || ItemId == 3042;

        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            foreach (short slot in new short[]
            {
                0,
                1,
                2,
                3
            }

            )
            {
                if (owner.Spells.TryGetValue(slot, out var spell))
                {
                    ApiEventManager.OnSpellCast.AddListener(this, spell, Cast);
                }
            }

            if (ItemId == 3008)
            {
                ApiEventManager.OnHitUnit.AddListener(this, owner, Hit);
            }

            Refresh();
            _lastMana = owner.Stats.CurrentMana;
        }

        private void Cast(Spell spell)
        {
            if (!spell.Script.ScriptMetadata.TriggersSpellCasts || _owner.IsDead || IsCharged)
            {
                return;
            }

            Charge();
            _castSpend = true;
        }

        private void Hit(DamageData data)
        {
            if (ItemEffectHelpers.IsSuccessfulBasicAttack(_owner, data))
            {
                Charge();
            }
        }

        private bool PreferredCharge
        {
            get
            {
                return new[]
                {
                    3007,
                    3008,
                    3073
                }.FirstOrDefault(_owner.Inventory.HasItemWithID) == ItemId;
            }
        }

        private void Charge()
        {
            if (!PreferredCharge)
            {
                return;
            }

            foreach (var item in _owner.Inventory.GetAllItems().Where(i => i.ItemData.ItemId == ItemId))
            {
                var state = item.GetState();
                var uses = state.GetInt("chargeUses");
                var stacks = state.GetInt("manaCharge");
                if (uses >= 2 || stacks >= 750)
                {
                    continue;
                }

                if (uses == 0)
                {
                    state.Set("chargeCooldown", 6f);
                }

                state.Set("chargeUses", uses + 1);
                state.Set("manaCharge", Math.Min(750, stacks + (ItemId == 3007 ? 10 : ItemId == 3008 ? 8 : 5)));
            }

            Refresh();
        }

        private void Refresh()
        {
            float previousMaxMana = _owner.Stats.ManaPoints.Total;
            _owner.RemoveStatModifier(StatsModifier);
            var own = _owner.Inventory.GetAllItems().Where(i => i.ItemData.ItemId == ItemId).ToArray();
            StatsModifier.ManaPoints.FlatBonus = IsCharged || !PreferredCharge
                || own.Length == 0 ? 0 : own.Max(i => i.GetState().GetInt("manaCharge"));
            _owner.AddStatModifier(StatsModifier);
            _owner.Stats.CurrentMana += Math.Max(0, _owner.Stats.ManaPoints.Total - previousMaxMana);
            float ap = (ItemId == 3007 && !_owner.Inventory.HasItemWithID(3040))
                || ItemId == 3040 ? _owner.Stats.ManaPoints.Total * 0.03f : 0;
            float ad = (ItemId == 3008 && !_owner.Inventory.HasItemWithID(3042))
                || ItemId == 3042 ? _owner.Stats.ManaPoints.Total * 0.02f : 0;
            _owner.RemoveStatModifier(StatsModifier);
            StatsModifier.AbilityPower.FlatBonus = ap;
            StatsModifier.AttackDamage.FlatBonus = ad;
            _owner.AddStatModifier(StatsModifier);
            foreach (var item in own)
            {
                var slot = (short)(_owner.Inventory.GetItemSlot(item) + 6);
                if (!_owner.Spells.TryGetValue(slot, out var spell))
                {
                    continue;
                }

                if (ItemId == 3007 || ItemId == 3040)
                {
                    ItemEffectHelpers.UpdateTooltip(_owner, item, 1, ap);
                }

                if (ItemId == 3008 || ItemId == 3042)
                {
                    ItemEffectHelpers.UpdateTooltip(_owner, item, 1, ad);
                }

                spell.ToolTipData.Update(0, item.GetState().GetInt("manaCharge"));
                if (_owner is Champion champion)
                {
                    champion.AddToolTipChange(spell.ToolTipData);
                }
            }
        }

        public void OnUpdate(float diff)
        {
            foreach (var item in _owner.Inventory.GetAllItems().Where(i => i.ItemData.ItemId == ItemId).ToArray())
            {
                var state = item.GetState();
                float remaining = Math.Max(0, state.GetFloat("chargeCooldown") - diff / 1000);
                state.Set("chargeCooldown", remaining);
                if (remaining <= 0)
                {
                    state.Set("chargeUses", 0);
                }

                if (ItemId == 3073)
                {
                    var elapsed = state.GetFloat("tearElapsed") + diff / 1000;
                    int ticks = (int)(elapsed / 6);
                    state.Set("tearElapsed", elapsed % 6);
                    state.Set("manaCharge", Math.Min(750, state.GetInt("manaCharge") + ticks));
                }
            }

            if (!IsCharged && !_castSpend && _owner.Stats.CurrentMana < _lastMana && !_owner.IsDead)
            {
                Charge();
            }

            _castSpend = false;
            Refresh();
            _lastMana = _owner.Stats.CurrentMana;
            if (ItemId != 3007 && ItemId != 3008)
            {
                return;
            }

            foreach (var item in _owner.Inventory.GetAllItems().Where(i => i.ItemData.ItemId == ItemId
                && i.GetState().GetInt("manaCharge") >= 750).ToArray())
            {
                var slot = _owner.Inventory.GetItemSlot(item);
                var state = item.GetState().Clone();
                var data = GetItemData(ItemId == 3007 ? 3040 : 3042);
                if (data == null)
                {
                    continue;
                }

                _owner.Inventory.RemoveItem(item, _owner);
                _owner.Inventory.AddItemToSlot(data, _owner, slot, state);
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3073 : ManaChargeItem
    {
        protected override int ItemId => 3073;
    }

    public class ItemID_3007 : ManaChargeItem
    {
        protected override int ItemId => 3007;
    }

    public class ItemID_3008 : ManaChargeItem
    {
        protected override int ItemId => 3008;
    }

    public class ItemID_3040 : ManaChargeItem
    {
        protected override int ItemId => 3040;
    }

    public class ItemID_3042 : ManaChargeItem
    {
        protected override int ItemId => 3042;
    }
}
