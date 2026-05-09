using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts
{
    internal class CharScriptDragon : ICharScript
    {
        public void OnActivate(ObjAIBase owner, Spell spell = null)
        {
            if (owner is Monster)
            {
                ApiEventManager.OnDeath.AddListener(this, owner, OnDeath, true);
            }
        }

        public void OnDeath(DeathData deathData)
        {
            var DragonKiller = deathData.Killer;

            foreach (var player in GetAllPlayersFromTeam(deathData.Killer.Team))
            {
                AddBuff("S5Test_DragonSlayerBuff", float.MaxValue, 1, null, player, deathData.Unit as Monster);
            }

            foreach (var unit in EnumerateUnitsInRange(deathData.Unit.Position, 1000f, true))
            {
                if (unit is Monster mons && mons.Name == "Dragon")
                {
                    mons.TakeDamage(mons, 100000.0f, DamageType.DAMAGE_TYPE_TRUE,
                        DamageSource.DAMAGE_SOURCE_INTERNALRAW, false);
                }
            }
        }
    }
}


namespace Buffs
{
    internal class S5Test_DragonSlayerBuff : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 5
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        Particle p;
        Buff thisBuff;


        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            thisBuff = buff;
            var owner = unit as ObjAIBase;

            // Always remove and re-add so stat changes take effect cleanly
            unit.RemoveStatModifier(StatsModifier);

            if (buff.StackCount >= 1)
            {
                StatsModifier.AttackDamage.PercentBonus = 0.08f;
                StatsModifier.AbilityPower.PercentBonus = 0.08f;
            }

            if (buff.StackCount >= 3)
            {
                StatsModifier.MoveSpeed.PercentBonus = 0.05f;
            }

            if (buff.StackCount >= 5)
            {
                // Aspect of the Dragon doubles all bonuses TODO: make it expire after 180 seconds?
                StatsModifier.AttackDamage.PercentBonus = 0.16f;
                StatsModifier.AbilityPower.PercentBonus = 0.16f;
                StatsModifier.MoveSpeed.PercentBonus = 0.10f;
            }

            unit.AddStatModifier(StatsModifier);

            // Add listeners only exactly at the stack they're introduced,
            // not on every subsequent OnActivate
            if (buff.StackCount == 2)
            {
                ApiEventManager.OnPreDealDamage.AddListener(this, unit, BonusDamageToMinions, false);
            }

            if (buff.StackCount == 4)
            {
                ApiEventManager.OnPreDealDamage.AddListener(this, unit, BonusDamageToTowers, false);
            }

            if (buff.StackCount == 5)
            {
                ApiEventManager.OnPreDealDamage.AddListener(this, unit, ApplyBurnDamage,
                    false); //TODO: make it expire after 180 seconds?
            }
        }

        private void BonusDamageToMinions(DamageData damageData)
        {
            var AdditionalMinionAndMonsterDamage = 1.15f;

            if (damageData.Target is Minion)
            {
                damageData.PostMitigationDamage *= AdditionalMinionAndMonsterDamage;
            }
        }

        private void BonusDamageToTowers(DamageData damageData)
        {
            var AdditionalTowerAndBuildingsDamage = 1.15f;

            if (damageData.Target is ObjBuilding)
            {
                damageData.PostMitigationDamage *= AdditionalTowerAndBuildingsDamage;
            }
        }

        private void ApplyBurnDamage(DamageData damageData)
        {
            var target = damageData.Target;
            var attacker = damageData.Attacker;
            var buffOwner = thisBuff.TargetUnit as ObjAIBase;

            if (damageData.Target is not ObjBuilding)
            {
                AddBuff("S5Test_DragonBurn", 3.0f, 1, null, target, buffOwner);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.RemoveStatModifier(StatsModifier);
            ApiEventManager.OnPreDealDamage.RemoveListener(this);
        }
    }
}


namespace Buffs
{
    internal class S5Test_DragonBurn : IBuffGameScript //todo: add the fire particle ig
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.RENEW_EXISTING,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        Buff thisBuff;
        AttackableUnit _unit;
        float _damageTimer = 0;

        // 150 true damage over 3 seconds = 50 per tick (every 1000ms)
        private const float TICK_DAMAGE = 50.0f;
        private const float TICK_INTERVAL = 1000.0f;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            thisBuff = buff;
            _unit = unit;
            _damageTimer = 0; // deal first tick immediately
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
        }

        public void OnUpdate(float diff)
        {
            if (thisBuff == null || thisBuff.SourceUnit == null || _unit == null)
                return;

            _damageTimer -= diff;
            if (_damageTimer <= 0)
            {
                _unit.TakeDamage(thisBuff.SourceUnit, TICK_DAMAGE, DamageType.DAMAGE_TYPE_TRUE,
                    DamageSource.DAMAGE_SOURCE_PERIODIC, false);
                _damageTimer = TICK_INTERVAL;
            }
        }
    }
}