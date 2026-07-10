using GameServerCore.Enums;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    internal class SummonerDot : IBuffGameScript
    {
        private Particle _ignite;
        private ObjAIBase _owner;
        private AttackableUnit _target;
        private float _damage;
        private Buff _buff;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = buff.SourceUnit;
            _target = unit;
            _buff = buff;
            _damage = 10 + _owner.Stats.Level * 4;
            _ignite = AddParticleTarget(_owner, unit, "Global_SS_Ignite", unit, buff.Duration, bone: "C_BUFFBONE_GLB_CHEST_LOC");
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            RemoveParticle(_ignite);
        }

        public void OnUpdate(Buff buff, float diff)
        {
            ExecutePeriodically(_buff.BuffVars, "igniteTick", 1000f, false, 0, () =>
            {
                _target.TakeDamage(_owner, _damage, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_SPELL, false);
            });
        }
    }
}
