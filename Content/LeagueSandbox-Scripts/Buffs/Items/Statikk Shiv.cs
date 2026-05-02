using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class ItemStatikShankCharge : IBuffGameScript
    {
        private const byte MAX_STACKS = 100;
        private const float PROC_DAMAGE = 100f;
        private const float CHAIN_RANGE = 550f;
        private const int MAX_EXTRA_TARGETS = 3;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = MAX_STACKS
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private ObjAIBase _owner;
        private Buff _self;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = unit as ObjAIBase;
            _self = buff;

            ApiEventManager.OnHitUnit.AddListener(this, _owner, OnHitUnit, false);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.OnHitUnit.RemoveListener(this);
        }

        private void OnHitUnit(DamageData data)
        {
            if (!data.IsAutoAttack) return;

            if (_self.StackCount < MAX_STACKS) return;

            var primary = data.Target;
            if (primary == null || primary.IsDead || primary.Stats == null || !primary.Stats.IsTargetable)
            {
                _self.SetStacks(1);
                return;
            }

            _self.SetStacks(1);
            ApplyShiv(primary, data);
            
            SpellDataFlags hitFlags = SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectEnemies;
            
            var extras = GetUnitsInRange(_owner, primary.Position, CHAIN_RANGE, true, hitFlags)
                .Where(u => u != null && u != primary && !u.IsDead) 
                .OrderBy(u => Vector2.DistanceSquared(u.Position, primary.Position))
                .Take(MAX_EXTRA_TARGETS);

            GameObject beamFrom = primary;
            foreach (var u in extras)
            {
                ApplyShiv(u, data);
                AddParticleTarget(_owner, beamFrom, "kennen_btl_beam.troy", u, 1.0f);
                beamFrom = u;
            }
        }

        private void ApplyShiv(AttackableUnit target, DamageData data)
        {
            if (target == null || target.IsDead) return;
            
            float dmg = PROC_DAMAGE;
            bool isCrit = new System.Random().NextDouble() < _owner.Stats.CriticalChance.Total;
            
            if (isCrit)
            {
                dmg *= _owner.Stats.CriticalDamage.Total;
            }
            if (_owner.Model == "Yasuo")
            {
                dmg *= 0.9f;
            }
            
            target.TakeDamage(_owner, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, isCrit);
            AddParticleTarget(_owner, target, "kennen_btl_tar.troy", target, 1.0f);
        }
    }
}