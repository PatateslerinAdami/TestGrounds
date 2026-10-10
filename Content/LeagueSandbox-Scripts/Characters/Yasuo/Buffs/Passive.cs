using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
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
    public class YasuoPassive : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.AURA,
            IsHidden = true,
            SendHiddenToClient = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();
    }

    public class YasuoPassiveMovementShield : YasuoPassive
    {
    }

    public class YasuoPassiveMSCharge : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COUNTER,
            MaxStacks = 100
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();
    }

    public class YasuoPassiveMSShieldOn : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Shield _shield;
        private Particle _effect;
        private Buff _buff;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            _shield = AddShield(buff.SourceUnit, unit, unit.Stats.ManaPoints.Total);
            _effect = AddParticleTarget(buff.SourceUnit, unit, "Yasuo_Base_passive_activate.troy", unit, lifetime: buff.Duration);
            AddParticleTarget(buff.SourceUnit, unit, "Yasuo_Base_Passive_Burst.troy", unit, lifetime: 0.25f);
            ApiEventManager.OnShieldBreak.AddListener(this, _shield, OnShieldBreak);
        }

        private void OnShieldBreak(Shield shield)
        {
            if (!_buff.Remove)
            {
                RemoveBuff(_buff);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            if (_shield != null)
            {
                RemoveShield(unit, _shield);
            }

            RemoveParticle(_effect);
            AddParticleTarget(buff.SourceUnit, unit, "Yasuo_Base_passive_destroy.troy", unit, lifetime: 0.25f);
        }
    }
}
