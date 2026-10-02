using System;
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
    public class ItemBlackfireTorchSpell : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_DEHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _unit;
        private ObjAIBase _owner;
        private Buff _buff;
        private Particle _debuffParticle;
        private Particle _markerParticle;
        private float _tickDamage;
        private float _elapsed;
        private int _ticks;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            _owner = buff.SourceUnit;
            _buff = buff;
            _tickDamage = unit.Stats.HealthPoints.Total * 0.025f;
            _debuffParticle = AddParticleTarget(
                _owner,
                unit,
                "obj_deathfiregrasp_debuff.troy",
                unit,
                buff.Duration,
                ignoreCasterVisibility: true
            );
            _markerParticle = AddParticleTarget(
                _owner,
                unit,
                "tt_blackfiretorch.troy",
                unit,
                buff.Duration,
                bone: "C_BUFFBONE_GLB_HEAD_LOC",
                ignoreCasterVisibility: true
            );
            ApiEventManager.OnPreTakeDamage.AddListener(this, unit, AmplifyMagicDamage);
        }

        private void AmplifyMagicDamage(DamageData data)
        {
            if (data.DamageType == DamageType.DAMAGE_TYPE_MAGICAL)
            {
                data.PostMitigationDamage *= 1.2f;
            }
        }

        public void OnUpdate(float diff)
        {
            if (_buff.Elapsed())
            {
                return;
            }

            if (_unit.IsDead)
            {
                RemoveBuff(_buff);
                return;
            }

            _elapsed = Math.Min(4.0f, _elapsed + (diff / 1000.0f));
            while (_ticks < 8 && _elapsed + 0.00001f >= (_ticks + 1) * 0.5f)
            {
                _ticks++;
                _unit.TakeDamage(
                    _owner,
                    _tickDamage,
                    DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_PERIODIC,
                    false
                );
                if (_unit.IsDead || _buff.Elapsed())
                {
                    break;
                }
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveParticle(_debuffParticle);
            RemoveParticle(_markerParticle);
        }
    }
}
