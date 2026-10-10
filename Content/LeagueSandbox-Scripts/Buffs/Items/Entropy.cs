using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemHelpers;
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
    public class OdinEntropicClaymore : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private Particle _particle;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = buff.SourceUnit;
            _particle = AddParticleTarget(
                _owner,
                unit,
                "neutralmonster_buf_red_offense.troy",
                unit,
                buff.Duration,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
            AddParticleTarget(_owner, unit, "spectral_fury_cas.troy", unit, 1.0f, ignoreCasterVisibility: true);
            ApiEventManager.OnHitUnit.AddListener(this, _owner, Hit);
        }

        private void Hit(DamageData data)
        {
            if (!ItemEffectHelpers.IsSuccessfulOnHit(_owner, data))
            {
                return;
            }

            if (data.Target is not ObjAIBase)
            {
                return;
            }

            if (data.Target is BaseTurret)
            {
                return;
            }

            if (data.Target is Minion { IsWard: true })
            {
                return;
            }

            var variables = new BuffVariables();
            variables.Set("slow", 0.3f);
            AddBuff("EntropySlow", 2.5f, 1, null, data.Target, _owner, buffVariables: variables);
            AddBuff("EntropyBurning", 2.5f, 1, null, data.Target, _owner);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveParticle(_particle);
        }
    }

    public class EntropySlow : ItemFrozenMalletSlow, IBuffGameScript
    {
        public new BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SLOW,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };
    }

    public class EntropyBurning : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _unit;
        private ObjAIBase _owner;
        private float _elapsed;
        private int _ticks;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _unit = unit;
            _owner = buff.SourceUnit;
        }

        public void OnUpdate(float diff)
        {
            if (_unit.IsDead)
            {
                return;
            }

            _elapsed += diff / 1000f;
            while (_ticks < 5 && _elapsed + 0.00001f >= (_ticks + 1) * 0.5f)
            {
                _ticks++;
                _unit.TakeDamage(_owner, 16, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_PERIODIC, false);
                if (_unit.IsDead)
                {
                    break;
                }
            }
        }
    }
}
