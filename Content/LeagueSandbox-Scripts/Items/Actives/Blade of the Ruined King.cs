using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ItemSwordOfFeastAndFamine : ItemActiveSpell
    {
        protected override float Cooldown => 90;

        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            if (target is not Champion || target.IsDead || target.Team == owner.Team
                || !target.GetIsTargetableToTeam(owner.Team)
                || Vector2.DistanceSquared(owner.Position, target.Position) > 550 * 550)
            {
                return false;
            }

            var haste = new BuffVariables();
            haste.Set("speedFlat", target.GetMoveSpeed() * 0.25f);
            var slow = new BuffVariables();
            slow.Set("slow", 0.25f);
            AddBuff("ItemSwordOfFeastAndFamine", 3, 1, spell, target, owner, buffVariables: slow);
            AddBuff("ItemSwordOfFeastAndFamineSelf", 3, 1, spell, owner, owner, buffVariables: haste);
            var damage = target.TakeDamage(
                owner,
                Math.Max(100, target.Stats.HealthPoints.Total * 0.1f),
                DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource.DAMAGE_SOURCE_PROC,
                false
            );
            AddParticleTarget(owner, owner, "piratecutlass_cas.troy", owner, 1f);
            owner.SetSpell(
                "ItemSwordOfFeastAndFamineTransfuse",
                (byte)SpellSlotType.TempItemSlot,
                true,
                networked: false
            );
            CreateCustomMissile(
                owner,
                0,
                SpellSlotType.TempItemSlot,
                target.Position,
                owner.Position,
                new MissileParameters { Type = MissileType.Target },
                target: owner
            );
            float healthBefore = owner.Stats.CurrentHealth;
            owner.TakeHeal(owner, damage.PostMitigationDamage, HealType.SelfHeal);
            if (owner.Stats.CurrentHealth > healthBefore)
            {
                AddParticleTarget(owner, owner, "vampheal.troy", owner, 1f);
            }

            return true;
        }
    }
}

namespace Buffs
{
    public class ItemSwordOfFeastAndFamine : ItemFrozenMalletSlow, IBuffGameScript
    {
        public ItemSwordOfFeastAndFamine()
        {
            BuffMetaData.IsHidden = false;
        }

        private Particle _slow;
        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            base.OnActivate(unit, buff, spell);
            _slow = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "global_slow.troy",
                unit,
                buff.Duration,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
        }

        public override void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            base.OnDeactivate(unit, buff, spell);
            RemoveParticle(_slow);
        }
    }

    public class ItemSwordOfFeastAndFamineSelf : ItemDistortionHaste, IBuffGameScript
    {
        public ItemSwordOfFeastAndFamineSelf()
        {
            BuffMetaData.IsHidden = false;
        }

        private Particle _haste;
        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            StatsModifier.MoveSpeed.FlatBonus = buff.Variables.GetFloat("speedFlat");
            unit.AddStatModifier(StatsModifier);
            _haste = AddParticleTarget(
                buff.SourceUnit,
                unit,
                "global_haste.troy",
                unit,
                buff.Duration,
                bone: "C_BUFFBONE_GLB_GROUND_LOC"
            );
        }

        public override void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            RemoveParticle(_haste);
        }
    }
}

namespace Spells
{
    public class ItemSwordOfFeastAndFamineTransfuse : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false,
            DoesntBreakShields = true
        };
    }
}
