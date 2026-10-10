using System;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemHelpers;
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
    public class ItemSpellbladeCooldown : ItemLifelineCooldown
    {
    }

    public class ItemSpellblade : IBuffGameScript
    {
        public ItemSpellblade()
        {
            BuffMetaData.IsHidden = true;
        }

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private Buff _buff;
        private string _presentation;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            _buff = buff;
            OnUpdate(0);
            ApiEventManager.OnHitUnit.AddListener(this, _owner, Hit);
        }

        public void OnUpdate(float diff)
        {
            int item = new[]
            {
                3078,
                3100,
                3025,
                3057
            }.FirstOrDefault(_owner.Inventory.HasItemWithID);
            if (_owner.IsDead || item == 0)
            {
                RemoveBuff(_buff);
                return;
            }

            string name = item == 3100 ? "LichBane" : item == 3025 ? "ItemFrozenFist" : "Sheen";
            if (_presentation == name)
            {
                NativeItemPresentation.Show(_buff, name);
                return;
            }

            NativeItemPresentation.Remove(_buff);
            _presentation = name;
            NativeItemPresentation.Show(_buff, name);
        }

        private void Hit(DamageData data)
        {
            if (!ItemEffectHelpers.IsSuccessfulOnHit(_owner, data) || data.Target is Minion { IsWard: true })
            {
                return;
            }

            // Check priority: Trinity Force (3078), Lich Bane (3100), Iceborn Gauntlet (3025), Sheen (3057)
            int item = new[]
            {
                3078,
                3100,
                3025,
                3057
            }.FirstOrDefault(_owner.Inventory.HasItemWithID);
            if (item == 0)
            {
                RemoveBuff(_buff);
                return;
            }

            var baseDamage = _owner.Stats.AttackDamage.BaseValue;
            float amount;
            DamageType type;
            if (item == 3078)
            {
                amount = 2f * baseDamage;
                type = DamageType.DAMAGE_TYPE_PHYSICAL;
            }
            else if (item == 3100)
            {
                amount = (0.75f * baseDamage) + (0.5f * _owner.Stats.AbilityPower.Total);
                type = DamageType.DAMAGE_TYPE_MAGICAL;
            }
            else if (item == 3025)
            {
                amount = 1.25f * baseDamage;
                type = DamageType.DAMAGE_TYPE_PHYSICAL;
            }
            else
            {
                amount = baseDamage;
                type = DamageType.DAMAGE_TYPE_PHYSICAL;
            }

            RemoveBuff(_buff);
            AddBuff("ItemSpellbladeCooldown", 1.5f, 1, null, _owner, _owner);
            data.Target.TakeDamage(_owner, amount, type, DamageSource.DAMAGE_SOURCE_PROC, false);
            AddParticleTarget(
                _owner,
                data.Target,
                "globalhit_physical.troy",
                data.Target,
                0.5f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC"
            );
            if (_owner.Inventory.HasItemWithID(3025))
            {
                var variables = new BuffVariables();
                variables.Set("position", data.Target.Position);
                variables.Set("radius", _owner.CharData.IsMelee ? 250f : 125f);
                AddBuff("ItemIcebornField", 2f, 1, null, _owner, _owner, buffVariables: variables);
                if (item == 3025)
                {
                    foreach (var unit in EnumerateUnitsInRange(data.Target.Position, 250f, true))
                    {
                        if (unit != data.Target && unit.Team != _owner.Team && unit is ObjAIBase
                            && unit is not BaseTurret
                            && unit is not Minion { IsWard: true })
                        {
                            unit.TakeDamage(_owner, amount, type, DamageSource.DAMAGE_SOURCE_PROC, false);
                        }
                    }
                }
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            NativeItemPresentation.Remove(buff);
        }
    }

    public class ItemIcebornField : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            IsHidden = true,
            BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
            MaxStacks = 100
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private Vector2 _position;
        private float _radius;
        private Particle _field;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = buff.SourceUnit;
            _position = buff.Variables.Get<Vector2>("position");
            _radius = buff.Variables.GetFloat("radius");
            string prefix = _radius < 250 ? "item_frozen_gauntlet_tiny_" : "item_frozen_gauntlet_";
            _field = AddParticlePos(
                _owner,
                prefix + "green.troy",
                _position,
                _position,
                0,
                enemyParticle: prefix + "red.troy",
                ignoreCasterVisibility: true
            );
            _field.isInfinite = true;
            Refresh();
        }

        private void Refresh()
        {
            foreach (var unit in EnumerateUnitsInRange(_position, _radius, true))
            {
                if (unit.Team == _owner.Team || unit is not ObjAIBase || unit is BaseTurret
                    || unit is Minion { IsWard: true })
                {
                    continue;
                }

                var variables = new BuffVariables();
                variables.Set("slow", 0.3f);
                AddBuff("ItemIcebornSlow", 0.3f, 1, null, unit, _owner, buffVariables: variables);
            }
        }

        public void OnUpdate(float diff)
        {
            Refresh();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_field);
        }
    }

    public class ItemIcebornSlow : ItemFrozenMalletSlow
    {
    }
}
