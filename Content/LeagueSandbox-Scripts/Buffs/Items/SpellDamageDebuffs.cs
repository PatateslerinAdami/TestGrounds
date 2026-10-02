using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using ItemHelpers;
using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public class ItemRylaiSlow : ItemFrozenMalletSlow
    {
    }

    public class BlackCleaverDebuff : IBuffGameScript
    {
        public BlackCleaverDebuff()
        {
            BuffMetaData.IsHidden = true;
        }

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 5
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private bool _applied;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            if (_applied)
            {
                unit.RemoveStatModifier(StatsModifier);
            }

            StatsModifier.Armor.PercentBonus = -0.05f * buff.StackCount;
            unit.AddStatModifier(StatsModifier);
            _applied = true;
            NativeItemPresentation.Show(buff, "BlackCleaver");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Remove(buff);
        }
    }

    public class EyesOfPain : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            StatsModifier.MagicPenetration.FlatBonus = 15;
            unit.AddStatModifier(StatsModifier);
        }
    }

    public class LiandrysTormentBurn : IBuffGameScript
    {
        public LiandrysTormentBurn()
        {
            BuffMetaData.IsHidden = true;
        }

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _unit;
        private ObjAIBase _source;
        private float _elapsed;
        private Buff _buff;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _unit = unit;
            _source = buff.SourceUnit;
            _buff = buff;
            NativeItemPresentation.Show(buff, "ZephyrsLamentBurn");
        }

        public void OnUpdate(float diff)
        {
            if (_unit.IsDead)
            {
                RemoveBuff(_buff);
                return;
            }

            _elapsed += Math.Min(diff / 1000f, Math.Max(0, _buff.Duration - _buff.TimeElapsed + (diff / 1000f)));
            while (_elapsed + 0.00001f >= 0.5f)
            {
                _elapsed -= 0.5f;
                bool hasHardCc = (_unit.Status & (StatusFlags.Rooted | StatusFlags.Stunned | StatusFlags.Taunted | StatusFlags.Feared)) != 0;
                bool isSlowed = _unit.GetBuffs().Exists(b => !b.Elapsed()
                    && b.BuffScript.BuffMetaData.BuffType == BuffType.SLOW);
                bool impaired = hasHardCc || isSlowed;
                var damage = _unit.Stats.CurrentHealth * (impaired ? 0.02f : 0.01f);
                if (_unit.Team == TeamId.TEAM_NEUTRAL)
                {
                    damage = Math.Min(50f, damage);
                }

                _unit.TakeDamage(
                    _source,
                    damage,
                    DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_PERIODIC,
                    false
                );
                if (_unit.IsDead)
                {
                    break;
                }
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Remove(buff);
        }
    }
}
