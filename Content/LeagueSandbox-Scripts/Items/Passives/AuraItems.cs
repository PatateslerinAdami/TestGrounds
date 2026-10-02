using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public abstract class AuraItem : UniqueStatItem
    {
        protected ObjAIBase Owner;
        protected abstract string AuraName { get; }
        protected abstract float Radius { get; }
        protected abstract bool Allied { get; }

        protected virtual bool ChampionsOnly => false;

        protected abstract int[] ProviderIds { get; }

        private float _elapsed;
        private Buff _presentation;
        protected virtual string SelfPresentation => null;

        private HashSet<AttackableUnit> _targets = new HashSet<AttackableUnit>();
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            Owner = owner;
            base.OnActivate(owner);
            Refresh();
            RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            if (SelfPresentation == null)
            {
                return;
            }

            if (Owner.IsDead)
            {
                if (_presentation != null)
                {
                    RemoveBuff(_presentation);
                }

                _presentation = null;
                return;
            }

            if (_presentation != null && !_presentation.Elapsed())
            {
                return;
            }

            var variables = new BuffVariables();
            variables.Set("nativeName", SelfPresentation);
            _presentation = AddBuff("ItemAuraPresentation", 0, 1, null, Owner, Owner, true, buffVariables: variables);
        }

        private bool InRange(AttackableUnit target)
        {
            return !target.IsDead && (target.Team == Owner.Team) == Allied && target is ObjAIBase
                && target is not BaseTurret
                && target is not Minion { IsWard: true }
                && (!ChampionsOnly
                || target is Champion);
        }

        private void Refresh()
        {
            var next = Owner.IsDead ? new HashSet<AttackableUnit>() : EnumerateUnitsInRange(
                Owner.Position,
                Radius,
                true
            ).Where(InRange).ToHashSet();
            foreach (var target in next)
            {
                AddBuff(AuraName, 0.5f, 1, null, target, Owner);
            }

            foreach (var target in _targets.Except(next))
            {
                Release(target);
            }

            _targets = next;
        }

        private void Release(AttackableUnit target)
        {
            var buff = target.GetBuffWithName(AuraName);
            if (buff?.SourceUnit != Owner)
            {
                return;
            }

            foreach (var unit in EnumerateUnitsInRange(target.Position, 1200, true).OfType<ObjAIBase>())
            {
                if (unit == Owner || unit.Team != Owner.Team)
                {
                    continue;
                }

                var id = ProviderIds.FirstOrDefault(unit.Inventory.HasItemWithID);
                if (id == 0)
                {
                    continue;
                }

                var radius = id == 3060 || id == 3001 || id == 3110 ? 700 : 1200;
                if (Vector2.DistanceSquared(unit.Position, target.Position) > radius * radius)
                {
                    continue;
                }

                AddBuff(AuraName, 0.5f, 1, null, target, unit);
                return;
            }

            NativeItemPresentation.Remove(buff);
            RemoveBuff(buff);
        }

        public override void OnUpdate(float diff)
        {
            RefreshPresentation();
            _elapsed += diff;
            if (_elapsed < 250)
            {
                return;
            }

            _elapsed %= 250;
            Refresh();
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            foreach (var target in _targets)
            {
                Release(target);
            }

            _targets.Clear();
            if (_presentation != null)
            {
                RemoveBuff(_presentation);
            }
        }
    }

    public class ItemID_3001 : AuraItem
    {
        protected override string AuraName => "AbyssalScepterAura";

        protected override string SelfPresentation => "AbyssalScepterAuraSelf";

        protected override float Radius => 700;

        protected override bool Allied => false;

        protected override int[] ProviderIds
        {
            get
            {
                return new[]
                {
                    3001
                };
            }
        }
    }

    public class ItemID_3050 : AuraItem
    {
        protected override string AuraName => "ZekesHeraldAura";

        protected override float Radius => 1200;

        protected override bool Allied => true;

        protected override bool ChampionsOnly => true;

        protected override int[] ProviderIds
        {
            get
            {
                return new[]
                {
                    3050
                };
            }
        }

        protected override void Configure()
        {
            StatsModifier.CooldownReduction.FlatBonus = -0.2f;
        }
    }

    public class ItemID_3105 : AuraItem
    {
        protected override string AuraName => "LegionAura";

        protected override float Radius => 1200;

        protected override bool Allied => true;

        protected override int[] ProviderIds
        {
            get
            {
                return new[]
                {
                    3105,
                    3190,
                    3060
                };
            }
        }
    }

    public class ItemID_3190 : ItemID_3105
    {
        public override void OnUpdate(float diff)
        {
            base.OnUpdate(diff);
            ItemEffectHelpers.UpdateTooltip(Owner, 3190, 0, 10f * Owner.Stats.Level);
        }
    }

    public class ItemID_3060 : ItemID_3105
    {
        protected override float Radius => 700;
    }

    public class ItemID_3110 : AuraItem
    {
        protected override string AuraName => "FrozenHeartAura";

        protected override string SelfPresentation => "FrozenHeartAuraCosmetic";

        protected override float Radius => 700;

        protected override bool Allied => false;

        protected override int[] ProviderIds
        {
            get
            {
                return new[]
                {
                    3110
                };
            }
        }

        protected override void Configure()
        {
            StatsModifier.CooldownReduction.FlatBonus = -0.2f;
        }
    }

    public class ItemID_3068 : UniqueStatItem
    {
        private ObjAIBase _owner;
        private float _elapsed;
        protected override void Configure()
        {
        }

        private Buff _presentation;
        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            if (_presentation != null)
            {
                RemoveBuff(_presentation);
            }
        }

        public override void OnUpdate(float diff)
        {
            ItemEffectHelpers.UpdateTooltip(_owner, 3068, 0, _owner.Stats.Level);
            if (_owner.IsDead)
            {
                if (_presentation != null)
                {
                    RemoveBuff(_presentation);
                }

                _presentation = null;
                return;
            }

            if (_presentation == null || _presentation.Elapsed())
            {
                var variables = new BuffVariables();
                variables.Set("nativeName", "ItemSunfireCapeParticle");
                _presentation = AddBuff(
                    "ItemAuraPresentation",
                    0,
                    1,
                    null,
                    _owner,
                    _owner,
                    true,
                    buffVariables: variables
                );
            }

            _elapsed += diff;
            while (_elapsed >= 1000)
            {
                _elapsed -= 1000;
                foreach (var unit in EnumerateUnitsInRange(_owner.Position, 400, true))
                {
                    if (unit.Team != _owner.Team && unit is ObjAIBase && unit is not BaseTurret
                        && unit is not Minion { IsWard: true })
                    {
                        unit.TakeDamage(
                            _owner,
                            25 + _owner.Stats.Level,
                            DamageType.DAMAGE_TYPE_MAGICAL,
                            DamageSource.DAMAGE_SOURCE_PERIODIC,
                            false
                        );
                        if (!unit.IsDead)
                        {
                            AddBuff("ItemSunfireCapeAura", 1.1f, 1, null, unit, _owner);
                        }
                    }
                }
            }
        }
    }
}
