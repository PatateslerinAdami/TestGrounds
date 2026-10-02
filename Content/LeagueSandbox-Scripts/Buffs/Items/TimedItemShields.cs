using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class TimedItemShield : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        protected Shield _shield;
        public float Remaining
        {
            get
            {
                if (_shield != null)
                {
                    return _shield.Amount;
                }

                return 0f;
            }
        }

        public virtual void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            float amount = buff.Variables.GetFloat("shield");
            bool isPhysicalAndMagic = !buff.Variables.GetBool("magicOnly");
            _shield = AddShield(buff.SourceUnit, unit, amount, isPhysicalAndMagic, true);
        }

        public virtual void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveShield(unit, _shield);
        }
    }

    public class ItemSeraphsEmbrace : TimedItemShield, IBuffGameScript
    {
        private Buff _buff;
        private Particle _particle;
        private float _lastAmount;
        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _buff = buff;
            base.OnActivate(unit, buff, spell);
            _lastAmount = Remaining;
            UpdateTooltip();
            _particle = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "seraphsembrace_shield.troy",
                unit,
                buff.Duration,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                ignoreCasterVisibility: true
            );
            ApiEventManager.OnTakeDamage.AddListener(this, unit, data => ShieldHit());
            ApiEventManager.OnShieldBreak.AddListener(this, _shield, shield =>
            {
                ShieldHit();
                RemoveBuff(_buff);
            });
        }

        private void UpdateTooltip()
        {
            if (_buff.ToolTipData.Values[0].Value != Remaining)
            {
                _buff.SetToolTipVar(0, Remaining);
            }
        }

        private void ShieldHit()
        {
            if (Remaining < _lastAmount)
            {
                AddParticleTarget(
                    _buff.SourceUnit,
                    _buff.TargetUnit,
                    "steamgolemshield_hit.troy",
                    _buff.TargetUnit,
                    1f,
                    bone: "C_BUFFBONE_GLB_CHEST_LOC",
                    ignoreCasterVisibility: true
                );
                _lastAmount = Remaining;
                UpdateTooltip();
            }
        }

        public void OnUpdate(float diff)
        {
            UpdateTooltip();
            if (_buff.TargetUnit.IsDead || Remaining <= 0)
            {
                RemoveBuff(_buff);
            }
        }

        public override void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveParticle(_particle);
            base.OnDeactivate(unit, buff, spell);
        }
    }

    public class ItemSeraphShield : ItemSeraphsEmbrace
    {
    }

    public class IronStylusBuff : TimedItemShield, IBuffGameScript
    {
        private Particle _particle;
        private Buff _buff;
        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _buff = buff;
            base.OnActivate(unit, buff, spell);
            _particle = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "ironstylus_buf.troy",
                unit,
                buff.Duration,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                ignoreCasterVisibility: true
            );
            ApiEventManager.OnShieldBreak.AddListener(this, _shield, shield => RemoveBuff(_buff));
        }

        public void OnUpdate(float diff)
        {
            if (_buff.TargetUnit.IsDead || Remaining <= 0.0f)
            {
                RemoveBuff(_buff);
            }
        }

        public override void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveParticle(_particle);
            base.OnDeactivate(unit, buff, spell);
        }
    }

    public class ItemLocketShield : IronStylusBuff
    {
    }

    public class ItemLifelineShield : TimedItemShield, IBuffGameScript
    {
        private Buff _buff;
        private float _lastAmount;
        public ItemLifelineShield()
        {
            BuffMetaData.IsHidden = true;
        }

        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _buff = buff;
            base.OnActivate(unit, buff, spell);
            _lastAmount = Remaining;
            ItemHelpers.NativeItemPresentation.Show(buff, Remaining > 250 ? "HexdrinkerMaw" : "Hexdrinker");
            ApiEventManager.OnTakeDamage.AddListener(this, unit, data => Hit());
            ApiEventManager.OnShieldBreak.AddListener(this, _shield, shield =>
            {
                Hit();
                RemoveBuff(buff);
            });
        }

        private void Hit()
        {
            if (Remaining >= _lastAmount)
            {
                return;
            }

            AddParticleTarget(
                _buff.SourceUnit,
                _buff.TargetUnit,
                "hextech_dmg_shield_onhit_01.troy",
                _buff.TargetUnit,
                1f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                ignoreCasterVisibility: true
            );
            _lastAmount = Remaining;
        }

        public void OnUpdate(float diff)
        {
            if (_buff.TargetUnit.IsDead || Remaining <= 0)
            {
                RemoveBuff(_buff);
            }
        }

        public override void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            ItemHelpers.NativeItemPresentation.Remove(buff);
            base.OnDeactivate(unit, buff, spell);
        }
    }

    public class ItemArdentCenser : IBuffGameScript
    {
        public ItemArdentCenser()
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
            ItemHelpers.NativeItemPresentation.Show(buff, "ItemAngelHandBuff");
            StatsModifier.AttackSpeed.FlatBonus = 0.25f;
            unit.AddStatModifier(StatsModifier);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ItemHelpers.NativeItemPresentation.Remove(buff);
        }
    }
}
