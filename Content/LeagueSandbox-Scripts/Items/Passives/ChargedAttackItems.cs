using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3087 : OnHitItem
    {
        private Vector2 _last;
        private float _charge;
        private Buff _counter;
        private void Display()
        {
            if (_counter == null || _counter.Elapsed())
            {
                _counter = AddBuff("ItemStatikShankCharge", 0, 1, null, Owner, Owner, true);
            }

            _counter.Variables.Set("charge", _charge);
            _counter.BuffScript.OnUpdate(0);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            base.OnDeactivate(owner);
            if (_counter != null)
            {
                RemoveBuff(_counter);
            }
        }

        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            _last = owner.Position;
            Display();
        }

        protected override void OnHit(DamageData data)
        {
            if (_charge < 100)
            {
                _charge = Math.Min(100, _charge + 10);
                Display();
                return;
            }

            _charge = 0;
            Display();
            float damage = data.DamageResultType == DamageResultType.RESULT_CRITICAL ? 100 * Owner.Stats.CriticalDamage.Total : 100;
            var visited = new HashSet<AttackableUnit>();
            var next = data.Target;
            AttackableUnit previous = Owner;
            for (int i = 0; i < 4 && next != null; i++)
            {
                AddParticleTarget(
                    Owner,
                    previous,
                    "kennen_btl_beam.troy",
                    next,
                    0.5f,
                    bone: "C_BUFFBONE_GLB_CHEST_LOC",
                    targetBone: "C_BUFFBONE_GLB_CHEST_LOC"
                );
                AddParticleTarget(Owner, next, "kennen_btl_tar.troy", next, 0.5f, bone: "C_BUFFBONE_GLB_CHEST_LOC");
                previous = next;
                visited.Add(next);
                next.TakeDamage(
                    Owner,
                    damage,
                    DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_PROC,
                    data.DamageResultType == DamageResultType.RESULT_CRITICAL
                );
                var position = next.Position;
                next = EnumerateUnitsInRange(
                    position,
                    450,
                    true
                ).Where(u => u.Team != Owner.Team && u is ObjAIBase && u is not BaseTurret
                    && u is not Minion { IsWard: true }
                    && !visited.Contains(u)
                    && u.GetIsTargetableToTeam(Owner.Team)).OrderBy(u => Vector2.DistanceSquared(u.Position, position)).FirstOrDefault();
            }
        }

        public override void OnUpdate(float diff)
        {
            float distance = Vector2.Distance(_last, Owner.Position);
            _last = Owner.Position;
            // I think teleports do not generate Static Charges.
            if (!Owner.IsDead && distance <= Math.Max(100, Owner.GetMoveSpeed() * diff / 1000 * 2))
            {
                _charge = Math.Min(100, _charge + distance / 24);
            }

            Display();
        }
    }

    public class ItemID_3124 : UniqueStatItem
    {
        private ObjAIBase _owner;
        private float _combat;
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            ApiEventManager.OnLaunchAttack.AddListener(this, owner, spell => Stack());
            ApiEventManager.OnAbilityCast.AddListener(this, owner, cast => Stack());

            ApiEventManager.OnDealDamage.AddListener(this, owner, Combat);
            ApiEventManager.OnTakeDamage.AddListener(this, owner, Combat);
        }

        private void Stack()
        {
            if (!_owner.IsDead)
            {
                AddBuff("GuinsoosRageblade", 8, 1, null, _owner, _owner);
            }
        }

        private void Combat(DamageData data)
        {
            if (data.PostMitigationDamage > 0 && data.Attacker.Team != data.Target.Team)
            {
                _combat = 5000;
            }

            if (!_owner.IsDead && _owner.Stats.CurrentHealth < _owner.Stats.HealthPoints.Total * 0.5f
                && !_owner.HasBuff("GuinsoosRageCooldown"))
            {
                AddBuff("GuinsoosRage", 0, 1, null, _owner, _owner, true);
                AddBuff("GuinsoosRageCooldown", 30, 1, null, _owner, _owner);
            }
        }

        public override void OnUpdate(float diff)
        {
            _combat = Math.Max(0, _combat - diff);
            if (_combat == 0 || _owner.IsDead)
            {
                RemoveBuff(_owner, "GuinsoosRage");
            }
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveBuff(owner, "GuinsoosRage");
            RemoveBuff(owner, "GuinsoosRageblade");
        }
    }

    public class ItemID_3131 : UniqueStatItem
    {
        private ObjAIBase _owner;
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            OnUpdate(0);
            ApiEventManager.OnKill.AddListener(this, owner, data =>
            {
                foreach (var item in owner.Inventory.GetAllItems().Where(i => i.ItemData.ItemId == 3131))
                {
                    var spell = owner.Spells[(short)(6 + owner.Inventory.GetItemSlot(item))];
                    spell.SetCooldown(spell.CurrentCooldown * 0.5f, true);
                }
            });
        }

        public override void OnUpdate(float diff)
        {
            float speed = _owner.Inventory.GetAllItems().Where(i => i.ItemData.ItemId == 3131).Any(i => _owner.Spells[(short)(6 + _owner.Inventory.GetItemSlot(i))].CurrentCooldown <= 0)
                && !_owner.HasBuff("ItemSoTD") ? 0.45f : 0;
            if (StatsModifier.AttackSpeed.FlatBonus == speed)
            {
                return;
            }

            _owner.RemoveStatModifier(StatsModifier);
            StatsModifier.AttackSpeed.FlatBonus = speed;
            _owner.AddStatModifier(StatsModifier);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveBuff(owner, "ItemSoTD");
        }
    }
}
