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
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class ItemFrozenMalletSlow : IBuffGameScript
    {
        public ItemFrozenMalletSlow()
        {
            BuffMetaData.IsHidden = true;
        }

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SLOW,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public virtual void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            StatsModifier.MoveSpeed.PercentBonus = -buff.Variables.GetFloat("slow", 0.3f);
            unit.AddStatModifier(StatsModifier);
            string name = buff.Name == "ItemCutlassSlow" ? "BilgewaterCutlass" : buff.Name == "ItemGunbladeSlow" ? "HextechGunbladeSpell" : buff.Name == "ItemRylaiSlow"
                || buff.Name == "ItemRanduinSlow"
                || buff.Name == "ItemRighteousGlorySlow" ? "Slow" : buff.Name == "ItemFrozenMalletSlow"
                || buff.Name == "ItemIcebornSlow" ? "ItemSlow" : null;
            if (name != null)
            {
                NativeItemPresentation.Show(buff, name);
            }
        }

        public virtual void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Remove(buff);
        }
    }

    public abstract class WitsEndResistance : IBuffGameScript
    {
        protected WitsEndResistance()
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
        protected abstract float PerStack { get; }

        private bool _applied;
        public virtual void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            if (_applied)
            {
                unit.RemoveStatModifier(StatsModifier);
            }

            StatsModifier.MagicResist.FlatBonus = PerStack * buff.StackCount;
            unit.AddStatModifier(StatsModifier);
            _applied = true;
            NativeItemPresentation.Show(buff, PerStack > 0 ? "WitsEndBuff" : "MaladyCounter");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            NativeItemPresentation.Remove(buff);
        }
    }

    public class ItemWitsEndDebuff : WitsEndResistance
    {
        protected override float PerStack => -5f;
    }

    public class ItemWitsEndBuff : WitsEndResistance
    {
        protected override float PerStack => 5f;
    }

    public class ItemWickedHatchetBleed : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _unit;
        private ObjAIBase _source;
        private Buff _buff;
        private float _damage;
        private float _elapsed;
        private int _ticks;
        private Particle _bleed;
        private Particle _reveal;
        public virtual void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _unit = unit;
            _source = buff.SourceUnit;
            _buff = buff;
            _damage = buff.Variables.GetFloat("damage") / 3f;
            _bleed = AddParticleTarget(
                _source,
                unit,
                "talon_q_bleed.troy",
                unit,
                0,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                ignoreCasterVisibility: true
            );
            _bleed.isInfinite = true;
            if (buff.Variables.GetBool("reveal"))
            {
                ItemVisionHelpers.Reveal(_source, unit, buff.Duration);
                _reveal = AddParticleTarget(
                    _source,
                    unit,
                    "tt_watched.troy",
                    unit,
                    0,
                    bone: "C_BUFFBONE_GLB_HEAD_LOC",
                    reqVision: false,
                    ignoreCasterVisibility: true
                );
                _reveal.isInfinite = true;
            }
        }

        public void OnUpdate(float diff)
        {
            if (_buff.Elapsed() || _unit.IsDead)
            {
                if (_unit.IsDead)
                {
                    RemoveBuff(_buff);
                }

                return;
            }

            _elapsed += diff / 1000f;
            while (_ticks < 3 && _elapsed + 0.00001f >= _ticks + 1)
            {
                _ticks++;
                _unit.TakeDamage(
                    _source,
                    _damage,
                    DamageType.DAMAGE_TYPE_PHYSICAL,
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
            RemoveParticle(_bleed);
            RemoveParticle(_reveal);
        }
    }

    public class ItemLightBringerBleed : ItemWickedHatchetBleed
    {
    }
}
