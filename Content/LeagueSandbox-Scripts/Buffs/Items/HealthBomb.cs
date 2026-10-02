using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class HealthBomb : TimedItemShield, GameServerCore.Scripting.CSharp.IBuffGameScript
    {
        private Buff _buff;
        private Particle _bubble, _bomb;
        private float _lastAmount;
        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _buff = buff;
            base.OnActivate(unit, buff, spell);
            _lastAmount = Remaining;
            _bubble = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "global_item_fom_shield.troy",
                unit,
                0,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                enemyParticle: "global_item_fom_shield_red.troy",
                ignoreCasterVisibility: true
            );
            _bomb = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "global_item_fom_bomb.troy",
                unit,
                0,
                bone: "C_BUFFBONE_GLB_HEAD_LOC",
                enemyParticle: "global_item_fom_bomb_red.troy",
                ignoreCasterVisibility: true
            );
            _bubble.isInfinite = _bomb.isInfinite = true;
            ApiEventManager.OnTakeDamage.AddListener(this, unit, data => Hit());

            ApiEventManager.OnShieldBreak.AddListener(this, _shield, shield =>
            {
                Hit();
                RemoveParticle(_bubble);
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
                "global_item_fom_hit.troy",
                _buff.TargetUnit,
                0.5f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                enemyParticle: "global_item_fom_hit_red.troy",
                ignoreCasterVisibility: true
            );
            _lastAmount = Remaining;
        }

        public void OnUpdate(float diff)
        {
            if (_buff.TargetUnit.IsDead)
            {
                RemoveBuff(_buff);
            }
        }

        public override void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveParticle(_bubble);
            RemoveParticle(_bomb);
            base.OnDeactivate(unit, buff, spell);
            if (unit.IsDead)
            {
                return;
            }

            if (buff.TimeElapsed < buff.Duration)
            {
                return;
            }

            AddParticleTarget(
                buff.SourceUnit,
                unit,
                "global_item_fom_detonate.troy",
                unit,
                1f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                enemyParticle: "global_item_fom_detonate_red.troy",
                ignoreCasterVisibility: true
            );
            var nearbyTargets = EnumerateUnitsInRange(unit.Position, 500, true);
            foreach (var target in nearbyTargets)
            {
                if (target.Team == unit.Team)
                {
                    continue;
                }

                if (target is not ObjAIBase)
                {
                    continue;
                }

                if (target is BaseTurret)
                {
                    continue;
                }

                if (target is Minion { IsWard: true })
                {
                    continue;
                }

                float baseDamage = unit.Stats.AttackDamage.Total;
                float apScaling = unit.Stats.AbilityPower.Total * 0.3f;
                float totalDamage = baseDamage + apScaling;
                AddParticleTarget(
                    buff.SourceUnit,
                    target,
                    "temp_supportdetonatehit.troy",
                    target,
                    0.5f,
                    bone: "C_BUFFBONE_GLB_CHEST_LOC"
                );
                target.TakeDamage(
                    buff.SourceUnit,
                    totalDamage,
                    DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_PROC,
                    false
                );
            }
        }
    }
}
