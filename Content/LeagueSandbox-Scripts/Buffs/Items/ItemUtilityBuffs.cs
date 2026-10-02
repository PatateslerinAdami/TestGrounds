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
    public class ItemColdSteel : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            StatsModifier.AttackSpeed.PercentBonus = -0.15f;
            unit.AddStatModifier(StatsModifier);
        }
    }

    public class CrippleSlow : ItemFrozenMalletSlow, IBuffGameScript
    {
        public new BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SLOW,
            BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
            MaxStacks = 100
        };
    }

    public class Cripple : ItemColdSteel
    {
    }

    public class ItemRanduinSlow : ItemFrozenMalletSlow
    {
    }

    public class ItemQuicksilverHaste : ItemDistortionHaste, IBuffGameScript
    {
        public ItemQuicksilverHaste()
        {
            BuffMetaData.IsHidden = false;
        }

        private Buff _buff;
        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _buff = buff;
            base.OnActivate(unit, buff, spell);
        }

        public void OnUpdate(float diff)
        {
            if (_buff.TargetUnit.IsDead)
            {
                RemoveBuff(_buff);
            }
        }
    }

    public class ItemDervishBlade : ItemQuicksilverHaste
    {
    }

    public class ItemMercurial : ItemQuicksilverHaste
    {
    }

    public class ItemDervishHaste : ItemDervishBlade
    {
    }

    public class ItemShurelyaHaste : ItemDistortionHaste
    {
    }

    public class ItemCutlassSlow : ItemFrozenMalletSlow
    {
    }

    public class ItemGunbladeSlow : ItemFrozenMalletSlow
    {
    }

    public class ItemGlacialSpikeEnemySlow : ItemFrozenMalletSlow, IBuffGameScript
    {
        public ItemGlacialSpikeEnemySlow()
        {
            BuffMetaData.IsHidden = false;
        }

        private AttackableUnit _unit;
        private Buff _buff;
        private Particle _chill;
        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _unit = unit;
            _buff = buff;
            base.OnActivate(unit, buff, spell);

            _chill = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "glacialspike_buf.troy",
                unit,
                buff.Duration,
                ignoreCasterVisibility: true
            );
        }

        public override void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            base.OnDeactivate(unit, buff, spell);
            RemoveParticle(_chill);
        }

        public void OnUpdate(float diff)
        {
            _unit.RemoveStatModifier(StatsModifier);
            float decayFactor = System.Math.Max(0, 1 - (_buff.TimeElapsed / _buff.Duration));
            StatsModifier.MoveSpeed.PercentBonus = -0.8f * decayFactor;
            _unit.AddStatModifier(StatsModifier);
        }
    }
}
