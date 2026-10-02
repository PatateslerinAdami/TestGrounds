using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class OdinQuestBuff : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.RENEW_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private AttackableUnit _unit;
        private Buff _buff;
        private Particle _particle;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            _buff = buff;
            _particle = AddParticleTarget(unit, unit, "odin_quest_buf", unit, -1.0f);
            ApiEventManager.OnPreDealDamage.AddListener(this, unit, OnPreDealDamage);
            ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
        }

        private void OnPreDealDamage(DamageData data)
        {
            if (_buff.Elapsed() || data.Attacker != _unit || data.Target == _unit
                || data.DamageSource == DamageSource.DAMAGE_SOURCE_INTERNALRAW)
            {
                return;
            }

            data.Damage *= 1.1f;
            data.PostMitigationDamage *= 1.1f;
        }

        private void OnDeath(DeathData data)
        {
            _buff.DeactivateBuff();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveParticle(_particle);
            _particle = null;
        }
    }
}
