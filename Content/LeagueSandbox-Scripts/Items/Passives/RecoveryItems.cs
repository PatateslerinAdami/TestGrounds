using System;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public abstract class ValorItem : UniqueStatItem
    {
        private ObjAIBase _owner;
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            ApiEventManager.OnLevelUp.AddListener(this, owner, Level);
        }

        private void Level(AttackableUnit unit)
        {
            if (!_owner.IsDead)
            {
                AddBuff("CatalystHeal", 8, 1, null, _owner, _owner);
            }
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3010 : ValorItem
    {
    }

    public class ItemID_3800 : ValorItem
    {
    }

    public class ItemID_3029 : ValorItem
    {
        private ObjAIBase _owner;
        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            base.OnActivate(owner);
            OnUpdate(0);
        }

        public override void OnUpdate(float diff)
        {
            int stacks = 0;
            foreach (var item in _owner.Inventory.GetAllItems())
            {
                if (item.ItemData.ItemId != 3029)
                {
                    continue;
                }

                var state = item.GetState();
                var age = Math.Min(400000, state.GetFloat("rodAge") + diff);
                state.Set("rodAge", age);
                int ownStacks = (int)(age / 40000);
                stacks += ownStacks;
                ItemHelpers.ItemEffectHelpers.UpdateTooltip(_owner, item, 0, ownStacks * 20f);
                ItemHelpers.ItemEffectHelpers.UpdateTooltip(_owner, item, 1, ownStacks * 20f);
                ItemHelpers.ItemEffectHelpers.UpdateTooltip(_owner, item, 2, ownStacks * 2f);
            }

            _owner.RemoveStatModifier(StatsModifier);
            StatsModifier.HealthPoints.FlatBonus = 20 * stacks;
            StatsModifier.ManaPoints.FlatBonus = 20 * stacks;
            StatsModifier.AbilityPower.FlatBonus = 2 * stacks;
            _owner.AddStatModifier(StatsModifier);
        }
    }

    public class ItemID_3028 : UniqueStatItem
    {
        protected ObjAIBase Owner;
        private float _elapsed;
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            Owner = owner;
            base.OnActivate(owner);
        }

        public override void OnUpdate(float diff)
        {
            if ((Owner.Inventory.HasItemWithID(3174) && this is not ItemID_3174) || (Owner.Inventory.HasItemWithID(3222)
                && this is not ItemID_3174
                && this is not ItemID_3222))
            {
                return;
            }

            if (Owner.IsDead)
            {
                return;
            }

            _elapsed += diff;
            while (_elapsed >= 5000)
            {
                _elapsed -= 5000;
                Owner.Stats.CurrentMana += Math.Max(0, Owner.Stats.ManaPoints.Total - Owner.Stats.CurrentMana) * 0.015f;
            }
        }
    }

    public class ItemID_3174 : ItemID_3028
    {
        protected override void Configure()
        {
            StatsModifier.CooldownReduction.FlatBonus = -0.2f;
        }

        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            ApiEventManager.OnKill.AddListener(this, owner, Restore);
            ApiEventManager.OnAssist.AddListener(this, owner, (unit, data) => Restore(data));
        }

        private void Restore(DeathData data)
        {
            Owner.Stats.CurrentMana = Math.Min(Owner.Stats.ManaPoints.Total, Owner.Stats.CurrentMana + Owner.Stats.ManaPoints.Total * 0.15f);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3211 : UniqueStatItem
    {
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            ApiEventManager.OnTakeDamage.AddListener(this, owner, Hit);
        }

        private void Hit(DamageData data)
        {
            if (data.Attacker is Champion && data.PostMitigationDamage > 0 && !data.Target.IsDead)
            {
                AddBuff("SpectresCowlRegen", 10, 1, null, data.Target, (ObjAIBase)data.Target);
            }
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveBuff(owner, "SpectresCowlRegen");
        }
    }
}
