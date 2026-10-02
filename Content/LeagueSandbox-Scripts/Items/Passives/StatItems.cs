using System;
using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives
{
    public abstract class UniqueStatItem : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        protected abstract void Configure();
        public virtual void OnActivate(ObjAIBase owner)
        {
            Configure();
            owner.AddStatModifier(StatsModifier);
        }

        public virtual void OnDeactivate(ObjAIBase owner)
        {
        }

        public virtual void OnUpdate(float diff)
        {
        }
    }

    public class ItemID_3024 : UniqueStatItem
    {
        protected override void Configure()
        {
            StatsModifier.CooldownReduction.FlatBonus = -0.1f;
        }
    }

    public class ItemID_3067 : ItemID_3024
    {
    }

    public class ItemID_3035 : UniqueStatItem
    {
        protected override void Configure()
        {
            StatsModifier.ArmorPenetration.PercentBaseBonus = 0.35f;
        }
    }

    public class ItemID_3134 : UniqueStatItem
    {
        protected override void Configure()
        {
            StatsModifier.CooldownReduction.FlatBonus = -0.1f;
            StatsModifier.ArmorPenetration.FlatBonus = 10;
        }
    }

    public class ItemID_3142 : UniqueStatItem
    {
        protected override void Configure()
        {
            StatsModifier.ArmorPenetration.FlatBonus = 20;
        }
    }

    public class ItemID_3504 : UniqueStatItem
    {
        private ObjAIBase _owner;
        protected override void Configure()
        {
            StatsModifier.MoveSpeed.PercentBonus = 0.08f;
        }

        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            base.OnActivate(owner);
            ApiEventManager.OnCastHeal.AddListener(this, owner, Heal);
            ApiEventManager.OnShieldAdded.AddListener(this, owner, Shield);
        }

        private void Grant(LeagueSandbox.GameServer.GameObjects.AttackableUnits.AttackableUnit target)
        {
            if (target != _owner && target.Team == _owner.Team && !target.IsDead)
            {
                LeagueSandbox.GameServer.API.ApiFunctionManager.AddBuff("ItemArdentCenser", 6, 1, null, target, _owner);
            }
        }

        private void Heal(HealData data)
        {
            if (data.HealAmount > 0 && data.HealType != HealType.HealthRegeneration)
            {
                Grant(data.Target);
            }
        }

        private void Shield(Shield shield)
        {
            Grant(shield.TargetUnit);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3065 : UniqueStatItem
    {
        protected override void Configure()
        {
            StatsModifier.CooldownReduction.FlatBonus = -0.1f;
        }

        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            ApiEventManager.OnReceiveHeal.AddListener(this, owner, Amplify);
        }

        private void Amplify(HealData data)
        {
            data.HealAmount *= 1.2f;
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3005 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            Update();
        }

        private void Update()
        {
            var damage = _owner.Stats.HealthPoints.Total * 0.015f;
            ItemHelpers.ItemEffectHelpers.UpdateTooltip(_owner, 3005, 1, damage);
            if (Math.Abs(damage - StatsModifier.AttackDamage.FlatBonus) < 0.0001f)
            {
                return;
            }

            _owner.RemoveStatModifier(StatsModifier);
            StatsModifier.AttackDamage.FlatBonus = damage;
            _owner.AddStatModifier(StatsModifier);
        }

        public void OnUpdate(float diff)
        {
            Update();
        }
    }

    public abstract class HexCoreItem : UniqueStatItem
    {
        protected abstract int PerLevel { get; }

        private ObjAIBase _owner;
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            Update();
            ApiEventManager.OnLevelUp.AddListener(this, owner, unit => Update());
        }

        private void Update()
        {
            _owner.RemoveStatModifier(StatsModifier);
            StatsModifier.AbilityPower.FlatBonus = PerLevel * _owner.Stats.Level;
            _owner.AddStatModifier(StatsModifier);
            if (_owner.Model == "Viktor")
            {
                int tier = PerLevel - 3, evolved = 0;
                for (int slot = 0; slot < 3; slot++)
                {
                    if ((_owner.Stats.EvolveFlags & (1u << slot)) != 0)
                    {
                        evolved++;
                    }
                }

                _owner.Stats.EvolvePoints = (byte)Math.Max(0, tier - evolved);
                if (tier == 3)
                {
                    _owner.Stats.EvolveFlags |= 8;
                }
            }
        }

        public override void OnUpdate(float diff)
        {
            Update();
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            if (owner.Model == "Viktor" && !new[]
            {
                3196,
                3197,
                3198
            }.Any(owner.Inventory.HasItemWithID))
            {
                owner.Stats.EvolvePoints = 0;
            }
        }
    }

    public class ItemID_3196 : HexCoreItem
    {
        protected override int PerLevel => 4;
    }

    public class ItemID_3197 : HexCoreItem
    {
        protected override int PerLevel => 5;
    }

    public class ItemID_3198 : HexCoreItem
    {
        protected override int PerLevel => 6;
    }
}
