using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using GameServerCore.Enums;
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
    internal class YasuoEBlock : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; private set; }

        private Particle _timer, _targetTimer;
        private Buff _buff;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            var owner = buff.SourceUnit;
            _timer = AddParticleTarget(owner, unit, $"Yasuo_base_E_timer{ownerSpell.CastInfo.SpellLevel}",
                unit, lifetime: buff.Duration, unitOnly: owner, reqVision: false);
            if (unit is Champion)
            {
                _targetTimer = AddParticleTarget(owner, unit, "Yasuo_E_timer_tar", unit,
                    lifetime: buff.Duration, unitOnly: unit, reqVision: false);
            }

            ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
            ApiEventManager.OnDeath.AddListener(this, owner, OnDeath);
        }

        private void RemoveTimers()
        {
            RemoveParticle(_timer);
            RemoveParticle(_targetTimer);
        }

        public void OnUpdate(float diff)
        {
            if (_buff.SourceUnit.IsToRemove())
            {
                RemoveBuff(_buff);
            }
        }

        private void OnDeath(DeathData data)
        {
            RemoveTimers();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveTimers();
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
