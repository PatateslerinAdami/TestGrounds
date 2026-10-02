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
    public class GuinsoosRageblade : IBuffGameScript
    {
        public GuinsoosRageblade()
        {
            BuffMetaData.IsHidden = true;
        }

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 8
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private bool _applied;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            if (_applied)
            {
                unit.RemoveStatModifier(StatsModifier);
            }

            StatsModifier.AttackSpeed.FlatBonus = 0.04f * buff.StackCount;
            StatsModifier.AbilityPower.FlatBonus = 4 * buff.StackCount;
            unit.AddStatModifier(StatsModifier);
            _applied = true;
            NativeItemPresentation.Show(buff, "Rageblade");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Remove(buff);
        }
    }

    public class GuinsoosRage : IBuffGameScript
    {
        public GuinsoosRage()
        {
            BuffMetaData.IsHidden = true;
        }

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            StatsModifier.AttackSpeed.FlatBonus = 0.2f;
            StatsModifier.LifeSteal.FlatBonus = 0.1f;
            StatsModifier.SpellVamp.FlatBonus = 0.1f;
            unit.AddStatModifier(StatsModifier);
            NativeItemPresentation.Show(buff, "RagebladeEnrage");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Remove(buff);
        }
    }

    public class GuinsoosRageCooldown : ItemLifelineCooldown
    {
    }

    public class ItemSoTD : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private Buff _buff;
        private int _hits;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            _buff = buff;
            StatsModifier.AttackSpeed.FlatBonus = 1;
            StatsModifier.CriticalChance.FlatBonus = 1;
            unit.AddStatModifier(StatsModifier);
            ApiEventManager.OnHitUnit.AddListener(this, _owner, Hit);
        }

        private void Hit(DamageData data)
        {
            if (ItemEffectHelpers.IsSuccessfulBasicAttack(_owner, data))
            {
                if (data.DamageResultType == DamageResultType.RESULT_CRITICAL)
                {
                    _hits++;
                    if (_hits == 3)
                    {
                        RemoveBuff(_buff);
                    }
                }
            }
        }

        public void OnUpdate(float diff)
        {
            if (_owner.IsDead)
            {
                RemoveBuff(_buff);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
