using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class FioraDuelist : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.AURA,
            IsHidden = true,
            SendHiddenToClient = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();
    }

    public class FioraDuelistHot : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            MaxStacks = 4
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _owner;
        private Buff _buff;
        private bool _installed;
        private Particle _healingEffect;
        private string _effectName;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = unit;
            _buff = buff;
            SyncRegeneration();
        }

        public void SyncRegeneration()
        {
            if (_buff == null || _buff.Remove)
            {
                return;
            }

            float rate = (7 + _owner.Stats.Level) * _buff.StackCount * 0.1666f;
            var effectName = _buff.StackCount > 1 ? "Fiora_heal2_buf.troy" : "Fiora_heal_buf.troy";
            if (_effectName != effectName)
            {
                RemoveParticle(_healingEffect);
                _effectName = effectName;
                _healingEffect = AddParticleTarget(_owner, _owner, effectName, _owner, lifetime: -1);
            }

            if (_installed && System.Math.Abs(StatsModifier.HealthRegeneration.FlatBonus - rate) < 0.0001f)
            {
                return;
            }

            if (_installed)
            {
                _owner.RemoveStatModifier(StatsModifier);
            }

            StatsModifier.HealthRegeneration.FlatBonus = rate;
            _owner.AddStatModifier(StatsModifier);
            _installed = true;
        }

        public void OnUpdate(float diff)
        {
            SyncRegeneration();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_healingEffect);
            _healingEffect = null;
            _effectName = null;
        }
    }

    public class FioraRiposteBuff : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.AURA,
            IsHidden = true,
            SendHiddenToClient = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _owner;
        private Spell _spell;
        private bool _installed;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = unit;
            _spell = spell;
            OnUpdate(0);
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, _ => OnUpdate(0));
        }

        public void OnUpdate(float diff)
        {
            float amount = _spell.CastInfo.SpellLevel == 0 ? 0 : _spell.SpellData.GetEffectAmount(1, _spell.CastInfo.SpellLevel);
            if (_installed && amount == StatsModifier.AttackDamage.FlatBonus)
            {
                return;
            }

            if (_installed)
            {
                _owner.RemoveStatModifier(StatsModifier);
            }

            StatsModifier.AttackDamage.FlatBonus = amount;
            _owner.AddStatModifier(StatsModifier);
            _installed = true;
        }
    }
}
