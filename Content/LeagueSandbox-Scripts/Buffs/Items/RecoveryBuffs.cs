using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class CatalystHeal : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HEAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Buff _buff;
        private Particle _particle;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _buff = buff;
            StatsModifier.HealthRegeneration.FlatBonus = 150f / 8f;
            if (unit.Stats.ParType == PrimaryAbilityResourceType.MANA)
            {
                StatsModifier.ManaRegeneration.FlatBonus = 200f / 8f;
            }

            unit.AddStatModifier(StatsModifier);
            _particle = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "env_manaheal.troy",
                unit,
                buff.Duration,
                ignoreCasterVisibility: true
            );
        }

        public void OnUpdate(float diff)
        {
            if (_buff.TargetUnit.IsDead)
            {
                RemoveBuff(_buff);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_particle);
        }
    }

    public class CatalystValor : CatalystHeal
    {
    }

    public class SpectresCowlRegen : IBuffGameScript
    {
        public SpectresCowlRegen()
        {
            BuffMetaData.IsHidden = true;
        }

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ItemHelpers.NativeItemPresentation.Show(buff, "ItemSpectreCowlRegen");
            StatsModifier.HealthRegeneration.PercentBaseBonus = 1;
            unit.AddStatModifier(StatsModifier);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ItemHelpers.NativeItemPresentation.Remove(buff);
        }
    }
}
