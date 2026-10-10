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
    public class TwoShivPoison : IBuffGameScript
    {
        private Particle _particle;
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SLOW,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            StatsModifier.MoveSpeed.PercentBonus = -buff.Variables.GetFloat("Slow");
            unit.AddStatModifier(StatsModifier);
            _particle = AddParticleTarget(buff.SourceUnit, unit, "global_slow", unit, buff.Duration);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _particle?.SetToRemove();
        }
    }

    public class TwoShivPoisonMiss : IBuffGameScript
    {
        private float _chance;
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _chance = buff.Variables.GetFloat("MissChance");
            unit.Stats.BasicAttackMissChance += _chance;
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.Stats.BasicAttackMissChance -= _chance;
        }
    }
}
