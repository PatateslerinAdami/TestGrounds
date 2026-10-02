using ItemHelpers;
using System;
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

namespace Buffs
{
    public class ItemBootEffects : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private Buff _buff;
        private float _combat;
        private float _auraTime;
        private bool HasBase(int id)
        {
            foreach (var item in _owner.Inventory.GetAllItems())
            {
                if (BootInfo.Bases.TryGetValue(item.ItemData.ItemId, out var baseId))
                {
                    if (baseId == id)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasEnchant(int type)
        {
            foreach (var item in _owner.Inventory.GetAllItems())
            {
                int itemId = item.ItemData.ItemId;
                if (BootInfo.Bases.ContainsKey(itemId))
                {
                    if (BootInfo.Enchantment(itemId) == type)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            _buff = buff;
            ApiEventManager.OnDealDamage.AddListener(this, unit, Deal);
            ApiEventManager.OnTakeDamage.AddListener(this, unit, Combat);
            ApiEventManager.OnPreTakeDamage.AddListener(this, unit, Block);
            short[] summonerSlots = new short[]
            { 4, 5 };
            foreach (short slot in summonerSlots)
            {
                if (_owner.Spells.TryGetValue(slot, out var summonerSpell))
                {
                    ApiEventManager.OnSpellPostCast.AddListener(this, summonerSpell, SummonerCast);
                }
            }

            Refresh();
        }

        private void Combat(DamageData data)
        {
            if (data.PostMitigationDamage > 0 && data.Attacker != data.Target)
            {
                _combat = 5f;
                Refresh();
            }
        }

        private void Deal(DamageData data)
        {
            Combat(data);
            bool isSpellOrAttack = (data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELL || data.IsAutoAttack);
            if (data.PostMitigationDamage > 0 && HasEnchant(2) && isSpellOrAttack)
            {
                AddBuff("ItemFuror", 2f, 1, null, _owner, _owner);
            }
        }

        private void Block(DamageData data)
        {
            if (HasBase(3047) && data.IsAutoAttack)
            {
                if (data.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK
                    && data.DamageType != DamageType.DAMAGE_TYPE_TRUE)
                {
                    data.PostMitigationDamage *= 0.9f;
                }
            }
        }

        private void SummonerCast(Spell spell)
        {
            if (!HasEnchant(3))
            {
                return;
            }

            if (spell.SpellName == "SummonerFlash" || spell.SpellName == "SummonerTeleport")
            {
                float bonusSpeed;
                float duration;
                if (spell.SpellName == "SummonerFlash")
                {
                    bonusSpeed = 0.2f;
                    duration = 1f;
                }
                else
                {
                    bonusSpeed = 0.3f;
                    duration = 3f;
                }

                var variables = new BuffVariables();
                variables.Set("speed", bonusSpeed);
                AddBuff("ItemDistortionHaste", duration, 1, spell, _owner, _owner, buffVariables: variables);
            }
        }

        public void Refresh()
        {
            var boots = _owner.Inventory.GetAllItems().Where(i => BootInfo.Bases.ContainsKey(i.ItemData.ItemId)).ToArray();
            if (boots.Length == 0)
            {
                RemoveBuff(_buff);
                return;
            }

            float sum = boots.Sum(i => i.ItemData.MoveSpeed.FlatBonus);
            float best = 0f;
            foreach (var item in boots)
            {
                int itemId = item.ItemData.ItemId;
                float moveSpeed = item.ItemData.MoveSpeed.FlatBonus;
                if (BootInfo.Bases[itemId] == 3117 && _combat > 0)
                {
                    moveSpeed -= 80f;
                }

                if (BootInfo.Enchantment(itemId) == 4)
                {
                    moveSpeed += 20f;
                }

                if (moveSpeed > best)
                {
                    best = moveSpeed;
                }
            }

            _owner.RemoveStatModifier(StatsModifier);
            StatsModifier.MoveSpeed.FlatBonus = best - sum;
            if (HasBase(3158))
            {
                StatsModifier.CooldownReduction.FlatBonus = -0.15f;
            }
            else
            {
                StatsModifier.CooldownReduction.FlatBonus = 0f;
            }

            if (HasBase(3009))
            {
                StatsModifier.SlowResistPercent = 0.25f;
            }
            else
            {
                StatsModifier.SlowResistPercent = 0f;
            }

            _owner.AddStatModifier(StatsModifier);
            _buff.Variables.Set("distortion", HasEnchant(3));
        }

        public void OnUpdate(float diff)
        {
            _combat = Math.Max(0, _combat - (diff / 1000f));
            Refresh();
            _auraTime += diff;
            if (_owner.IsDead || _auraTime < 250 || !HasEnchant(1))
            {
                return;
            }

            _auraTime %= 250;
            var nearbyUnits = EnumerateUnitsInRange(_owner.Position, 1200, true);
            foreach (var unit in nearbyUnits.OfType<Champion>())
            {
                bool isAllied = (unit != _owner && unit.Team == _owner.Team);
                bool isMoving = (unit.Waypoints.Count > 1);
                if (isAllied && isMoving)
                {
                    var dest = unit.Waypoints.Last();
                    float distFromDest = System.Numerics.Vector2.DistanceSquared(dest, _owner.Position);
                    float distFromCurrent = System.Numerics.Vector2.DistanceSquared(unit.Position, _owner.Position);
                    if (distFromDest < distFromCurrent)
                    {
                        AddBuff("ItemCaptain", 0.3f, 1, null, unit, _owner);
                    }
                }
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemTenacity : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            StatsModifier.Tenacity.FlatBonus = 0.35f;
            unit.AddStatModifier(StatsModifier);
        }
    }

    public class ItemFuror : IBuffGameScript
    {
        public ItemFuror()
        {
            BuffMetaData.IsHidden = true;
        }

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HASTE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _unit;
        private Buff _buff;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _unit = unit;
            _buff = buff;
            StatsModifier.MoveSpeed.PercentBonus = 0.12f;
            unit.AddStatModifier(StatsModifier);
            NativeItemPresentation.Show(buff, "BootsDeathmarchSpeed");
        }

        public void OnUpdate(float diff)
        {
            _unit.RemoveStatModifier(StatsModifier);
            float decayFactor = Math.Max(0, 1 - (_buff.TimeElapsed / 2f));
            StatsModifier.MoveSpeed.PercentBonus = 0.12f * decayFactor;
            _unit.AddStatModifier(StatsModifier);
        }

        public virtual void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Remove(buff);
        }
    }

    public class ItemCaptain : IBuffGameScript
    {
        public ItemCaptain()
        {
            BuffMetaData.IsHidden = true;
        }

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HASTE,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public virtual void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            StatsModifier.MoveSpeed.PercentBonus = 0.1f;
            unit.AddStatModifier(StatsModifier);
            NativeItemPresentation.Show(buff, "BootsCaptainSpeedVFX");
        }

        public virtual void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Remove(buff);
        }
    }

    public class ItemDistortionHaste : IBuffGameScript
    {
        public ItemDistortionHaste()
        {
            BuffMetaData.IsHidden = true;
        }

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HASTE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public virtual void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            string name = buff.Name == "ItemShurelyaHaste" ? "ShurelyaMinionHaste" : buff.Name == "ElixirIronPath" ? "ElixirOfIronTrailBuff" : buff.Name == "ItemDistortionHaste" ? "BootsDistortion" : null;
            if (name != null)
            {
                NativeItemPresentation.Show(buff, name);
            }

            StatsModifier.MoveSpeed.PercentBonus = buff.Variables.GetFloat("speed");
            unit.AddStatModifier(StatsModifier);
        }

        public virtual void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Remove(buff);
        }
    }
}
