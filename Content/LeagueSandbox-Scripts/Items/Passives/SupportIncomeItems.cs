using System;
using System.Linq;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public abstract class FavorItem : UniqueStatItem
    {
        protected abstract int ItemId { get; }

        private ObjAIBase _owner;
        protected override void Configure()
        {
            if (ItemId != 3301)
            {
                StatsModifier.GoldPerSecond.FlatBonus = 0.2f;
            }

        }

        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            base.OnActivate(owner);
            ApiEventManager.OnNearbyDeath.AddListener(this, owner, Death);
            if (owner is Champion champion)
            {
                ApiEventManager.OnAmbientGold.AddListener(
                    this,
                    champion,
                    gold => ItemEffectHelpers.TrackGold(owner, ItemId, "ambientGoldEarned", StatsModifier.GoldPerSecond.FlatBonus, 0)
                );
            }

            OnUpdate(0);
        }

        private void Death(DeathData data)
        {
            if (_owner.IsDead || data.Unit is not Minion { IsLaneMinion: true } || data.Killer == _owner
                || data.Unit.Team == _owner.Team
                || new[]
            {
                3069,
                3096,
                3301
            }.FirstOrDefault(_owner.Inventory.HasItemWithID) != ItemId)
            {
                return;
            }

            if (_owner is Champion champion)
            {
                champion.AddGold(champion, ItemId == 3301 ? 2 : 3);
            }

            _owner.TakeHeal(_owner, ItemId == 3069 ? 10 : 5, HealType.SelfHeal);
            ItemEffectHelpers.TrackGold(_owner, ItemId, "favorGoldEarned", ItemId == 3301 ? 2 : 3, 1);
        }

        public override void OnUpdate(float diff)
        {
            ItemEffectHelpers.TrackGold(_owner, ItemId, "ambientGoldEarned", 0, 0);
            ItemEffectHelpers.TrackGold(_owner, ItemId, "favorGoldEarned", 0, 1);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3301 : FavorItem
    {
        protected override int ItemId => 3301;
    }

    public class ItemID_3096 : FavorItem
    {
        protected override int ItemId => 3096;
    }

    public class ItemID_3069 : FavorItem
    {
        protected override int ItemId => 3069;
    }

    public abstract class TributeItem : UniqueStatItem
    {
        protected abstract int ItemId { get; }

        private ObjAIBase _owner;
        private int _charges = 3;
        private float _elapsed, _disabled;
        protected override void Configure()
        {
            StatsModifier.GoldPerSecond.FlatBonus = 0.2f;
        }

        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            base.OnActivate(owner);
            if (owner is Champion champion)
            {
                ApiEventManager.OnAmbientGold.AddListener(
                    this,
                    champion,
                    gold => ItemEffectHelpers.TrackGold(owner, ItemId, "ambientGoldEarned", StatsModifier.GoldPerSecond.FlatBonus, 0)
                );
            }

            Display();
            ApiEventManager.OnDealDamage.AddListener(this, owner, Damage);
            ApiEventManager.OnKillUnit.AddListener(this, owner, death =>
            {
                if (death.Unit is Minion { IsLaneMinion: true } && ItemId != 3092)
                {
                    _disabled = 12000;
                    Display();
                }
            });
        }

        private void Damage(DamageData data)
        {
            if (_owner.IsDead || data.PostMitigationDamage <= 0 || _charges == 0 || _disabled > 0
                || data.Target.Team == _owner.Team
                || (data.Target is not Champion
                && data.Target is not BaseTurret)
                || (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK
                && !SpellDamageItem.IsSpell(data))
                || new[]
            {
                3092,
                3098,
                3303
            }.FirstOrDefault(_owner.Inventory.HasItemWithID) != ItemId)
            {
                return;
            }

            _charges--;
            ItemEffectHelpers.TrackGold(_owner, ItemId, "tributeGoldEarned", ItemId == 3303 ? 5 : 10, 1);
            Display();
            if (_owner is Champion champion)
            {
                champion.AddGold(champion, ItemId == 3303 ? 5 : 10);
            }

            data.Target.TakeDamage(
                _owner,
                ItemId == 3303 ? 10 : 15,
                DamageType.DAMAGE_TYPE_MAGICAL,
                DamageSource.DAMAGE_SOURCE_PROC,
                false
            );
        }

        public override void OnUpdate(float diff)
        {
            _disabled = Math.Max(0, _disabled - diff);
            Display();
            if (_charges >= 3)
            {
                _elapsed = 0;
                return;
            }

            _elapsed += diff;
            while (_elapsed >= 10000 && _charges < 3)
            {
                _elapsed -= 10000;
                _charges++;
            }

            Display();
        }

        private void Display()
        {
            ItemEffectHelpers.ShowCharges(_owner, ItemId, _disabled > 0 ? 0 : _charges);
            ItemEffectHelpers.TrackGold(_owner, ItemId, "ambientGoldEarned", 0, 0);
            ItemEffectHelpers.TrackGold(_owner, ItemId, "tributeGoldEarned", 0, 1);
            if (ItemId == 3092)
            {
                ItemEffectHelpers.UpdateTooltip(_owner, ItemId, 2, 50f + 5f * _owner.Stats.Level);
            }
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3303 : TributeItem
    {
        protected override int ItemId => 3303;
    }

    public class ItemID_3098 : TributeItem
    {
        protected override int ItemId => 3098;
    }

    public class ItemID_3092 : TributeItem
    {
        protected override int ItemId => 3092;
    }

    public abstract class SpoilsItem : OnHitItem
    {
        protected abstract int ItemId { get; }

        private int _charges;
        private float _elapsed;
        private readonly System.Collections.Generic.Dictionary<LeagueSandbox.GameServer.GameObjects.AttackableUnits.AttackableUnit, Champion> _executed = new();
        private int MaxCharges
        {
            get
            {
                return ItemId == 3401 ? 4 : ItemId == 3097 ? 3 : 2;
            }
        }

        private float Period
        {
            get
            {
                return ItemId == 3302 ? 60000 : 30000;
            }
        }

        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            _charges = MaxCharges;

            ApiEventManager.OnKillUnit.AddListener(this, owner, Kill);
            Display();
        }

        private Champion Ally()
        {
            return EnumerateUnitsInRange(
                Owner.Position,
                1100,
                true
            ).OfType<Champion>().Where(c => c != Owner && c.Team == Owner.Team
                && !c.IsDead).OrderBy(c => System.Numerics.Vector2.DistanceSquared(c.Position, Owner.Position)).FirstOrDefault();
        }

        private bool Ready
        {
            get
            {
                return _charges > 0 && !Owner.IsDead && new[]
                {
                    3401,
                    3097,
                    3302
                }.FirstOrDefault(Owner.Inventory.HasItemWithID) == ItemId;
            }
        }

        protected override void OnHit(DamageData data)
        {
            if (!Ready || !Owner.CharData.IsMelee || data.Target is not Minion { IsLaneMinion: true }
                || data.Target.Stats.CurrentHealth >= 200
                || Ally() == null)
            {
                return;
            }

            _executed[data.Target] = Ally();
            _charges--;
            Display();
            data.Target.TakeDamage(
                Owner,
                data.Target.Stats.CurrentHealth + 1,
                DamageType.DAMAGE_TYPE_TRUE,
                DamageSource.DAMAGE_SOURCE_INTERNALRAW,
                false
            );
        }

        private void Kill(DeathData data)
        {
            if (data.Unit is not Minion { IsLaneMinion: true })
            {
                return;
            }

            if (!_executed.Remove(data.Unit, out var ally))
            {
                if (!Ready || data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK)
                {
                    return;
                }

                ally = Ally();
                if (ally == null)
                {
                    return;
                }

                _charges--;
            }

            float heal = ItemId == 3302 ? 40 : 50;
            if (ItemId == 3401)
            {
                heal += Owner.Stats.HealthPoints.Total * 0.01f;
            }

            Owner.TakeHeal(Owner, heal, HealType.SelfHeal);
            ally.TakeHeal(Owner, heal, HealType.OutgoingHeal);
            ally.AddGold(ally, data.Unit.Stats.GoldGivenOnDeath.Total);
            ItemEffectHelpers.TrackGold(Owner, ItemId, "spoilsGoldShared", data.Unit.Stats.GoldGivenOnDeath.Total, 0);
            Display();
        }

        public override void OnUpdate(float diff)
        {
            Display();
            if (_charges >= MaxCharges)
            {
                _elapsed = 0;
                return;
            }

            _elapsed += diff;
            while (_elapsed >= Period && _charges < MaxCharges)
            {
                _elapsed -= Period;
                _charges++;
            }

            Display();
        }

        private void Display()
        {
            ItemEffectHelpers.ShowCharges(Owner, ItemId, _charges);
            ItemEffectHelpers.TrackGold(Owner, ItemId, "spoilsGoldShared", 0, 0);
            if (ItemId == 3401)
            {
                ItemEffectHelpers.UpdateTooltip(Owner, ItemId, 4, Owner.Stats.HealthPoints.Total * 0.1f);
            }
            else
            {
                ItemEffectHelpers.UpdateTooltip(Owner, ItemId, 2, 200);
            }
        }
    }

    public class ItemID_3302 : SpoilsItem
    {
        protected override int ItemId => 3302;
    }

    public class ItemID_3097 : SpoilsItem
    {
        protected override int ItemId => 3097;
    }

    public class ItemID_3401 : SpoilsItem
    {
        protected override int ItemId => 3401;
    }
}
