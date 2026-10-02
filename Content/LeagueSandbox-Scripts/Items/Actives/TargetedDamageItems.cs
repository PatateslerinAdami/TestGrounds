using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class BilgewaterCutlass : ItemActiveSpell
    {
        protected override float Cooldown => 90;

        protected virtual float Damage(ObjAIBase owner)
        {
            return 100;
        }

        protected virtual float CastRange => 500;

        protected virtual float Slow => 0.25f;

        protected virtual string SlowName => "ItemCutlassSlow";

        protected virtual void ShowCast(ObjAIBase owner, AttackableUnit target)
        {
            AddParticleTarget(
                owner,
                owner,
                "piratecutlass_cas.troy",
                target,
                1f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC",
                targetBone: "C_BUFFBONE_GLB_CHEST_LOC"
            );
        }

        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            if (target is not Champion)
            {
                return false;
            }

            if (target is BaseTurret)
            {
                return false;
            }

            if (target is Minion { IsWard: true })
            {
                return false;
            }

            if (target.IsDead)
            {
                return false;
            }

            if (target.Team == owner.Team)
            {
                return false;
            }

            if (!target.GetIsTargetableToTeam(owner.Team))
            {
                return false;
            }

            if (Vector2.DistanceSquared(owner.Position, target.Position) > CastRange * CastRange)
            {
                return false;
            }

            ShowCast(owner, target);
            target.TakeDamage(
                owner,
                Damage(owner),
                DamageType.DAMAGE_TYPE_MAGICAL,
                DamageSource.DAMAGE_SOURCE_PROC,
                false
            );
            var variables = new BuffVariables();
            variables.Set("slow", Slow);
            if (!target.IsDead)
            {
                AddBuff(SlowName, 2, 1, spell, target, owner, buffVariables: variables);
            }

            return true;
        }
    }

    public class HextechGunblade : BilgewaterCutlass
    {
        protected override float Cooldown => 60;

        protected override float Damage(ObjAIBase owner)
        {
            return 150 + (0.4f * owner.Stats.AbilityPower.Total);
        }

        protected override float CastRange => 700;

        protected override float Slow => 0.4f;

        protected override string SlowName => "ItemGunbladeSlow";

        protected override void ShowCast(ObjAIBase owner, AttackableUnit target)
        {
            AddParticleTarget(
                owner,
                owner,
                "hextech_gunblade_cas.troy",
                owner,
                1f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC"
            );

            owner.SetSpell("HextechGunbladeSpell", (byte)SpellSlotType.TempItemSlot, true, networked: false);
            var missile = CreateCustomMissile(
                owner,
                0,
                SpellSlotType.TempItemSlot,
                owner.Position,
                target.Position,
                new MissileParameters { Type = MissileType.Target },
                target: target
            );

            if (missile == null)
            {
                ShowImpact(owner, target);
                return;
            }

            ApiEventManager.OnSpellMissileHit.AddListener(
                missile,
                missile,
                (shot, unit) => ShowImpact(owner, unit),
                true
            );
            ApiEventManager.OnSpellMissileEnd.AddListener(
                missile,
                missile,
                ended => ApiEventManager.RemoveAllListenersForOwner(ended),
                true
            );
        }

        private static void ShowImpact(ObjAIBase owner, AttackableUnit target)
        {
            AddParticleTarget(
                owner,
                target,
                "hextech_gunblade_tar.troy",
                target,
                1f,
                bone: "C_BUFFBONE_GLB_CHEST_LOC"
            );
        }
    }

    public class shurelyascrest : ItemActiveSpell
    {
        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            AddParticleTarget(
                owner,
                owner,
                "shurelyascrest_cas.troy",
                owner,
                1f,
                bone: "C_BUFFBONE_GLB_GROUND_LOC",
                ignoreCasterVisibility: true
            );
            var allies = EnumerateUnitsInRange(owner.Position, 600, true);
            foreach (var ally in allies)
            {
                if (ally is not Champion || ally.Team != owner.Team)
                {
                    continue;
                }

                var variables = new BuffVariables();
                variables.Set("speed", 0.4f);
                AddBuff("ItemShurelyaHaste", 3, 1, spell, ally, owner, buffVariables: variables);
            }

            return true;
        }
    }

    public class ItemGlacialSpikeCast : ItemActiveSpell
    {
        protected override bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position)
        {
            owner.SetSpell("ItemGlacialSpikeEnemy", (byte)SpellSlotType.TempItemSlot, true, networked: false);
            var missile = CreateCustomMissile(
                owner,
                0,
                SpellSlotType.TempItemSlot,
                owner.Position,
                position,
                new MissileParameters { Type = MissileType.Circle }
            );
            if (missile == null)
            {
                return false;
            }

            ApiEventManager.OnSpellMissileEnd.AddListener(
                missile,
                missile,
                ended => Explode(owner, spell, ended.Position),
                true
            );
            return true;
        }

        private static void Explode(ObjAIBase owner, Spell spell, Vector2 position)
        {
            AddParticlePos(owner, "item_trueice_hit_nova.troy", position, position, 1f, ignoreCasterVisibility: true);
            var units = EnumerateUnitsInRange(position, 275, true);
            foreach (var unit in units)
            {
                if (unit.Team == owner.Team)
                {
                    continue;
                }

                if (unit is not ObjAIBase)
                {
                    continue;
                }

                if (unit is BaseTurret)
                {
                    continue;
                }

                if (unit is Minion { IsWard: true })
                {
                    continue;
                }

                float baseDamage = 50 + (5 * owner.Stats.Level);
                unit.TakeDamage(
                    owner,
                    baseDamage,
                    DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_PROC,
                    false
                );
                if (unit.IsDead)
                {
                    continue;
                }

                var variables = new BuffVariables();
                variables.Set("slow", 0.8f);
                AddParticleTarget(owner, unit, "item_trueice_hit.troy", unit, 1f, ignoreCasterVisibility: true);
                AddBuff("ItemGlacialSpikeEnemySlow", 2, 1, spell, unit, owner, buffVariables: variables);
            }
        }
    }
}

namespace Spells
{
    public class HextechGunbladeSpell : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false,
            DoesntBreakShields = true,
            TriggersSpellCasts = false
        };
    }

    public class ItemGlacialSpikeEnemy : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false
        };
    }
}
