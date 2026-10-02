using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public class SummonerPromoteSR : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            var minion = (LaneMinion)unit;
            if (minion.MinionSpawnType == MinionSpawnType.MINION_TYPE_CASTER)
            {
                StatsModifier.HealthPoints.FlatBonus = 400;
            }
            else
            {
                StatsModifier.HealthPoints.FlatBonus = 600;
            }

            if (minion.MinionSpawnType == MinionSpawnType.MINION_TYPE_CANNON)
            {
                StatsModifier.AttackDamage.FlatBonus = 100;
            }
            else if (minion.MinionSpawnType == MinionSpawnType.MINION_TYPE_CASTER)
            {
                StatsModifier.AttackDamage.FlatBonus = 75;
            }
            else
            {
                StatsModifier.AttackDamage.FlatBonus = 50;
            }

            if (minion.MinionSpawnType == MinionSpawnType.MINION_TYPE_CANNON)
            {
                StatsModifier.Armor.FlatBonus = 100;
                StatsModifier.MagicResist.FlatBonus = 100;
            }
            else
            {
                StatsModifier.Armor.FlatBonus = 40;
                StatsModifier.MagicResist.FlatBonus = 40;
            }

            if (minion.MinionSpawnType == MinionSpawnType.MINION_TYPE_CASTER)
            {
                StatsModifier.AttackSpeed.FlatBonus = 0.3f;
            }
            else if (minion.MinionSpawnType == MinionSpawnType.MINION_TYPE_MELEE)
            {
                StatsModifier.AttackSpeed.FlatBonus = 0.9f;
            }
            else
            {
                StatsModifier.AttackSpeed.FlatBonus = 0f;
            }

            StatsModifier.Size.PercentBonus = 0.5f;
            unit.AddStatModifier(StatsModifier);
            unit.Stats.CurrentHealth = unit.Stats.HealthPoints.Total;
            buff.SetStatusEffect(StatusFlags.MagicImmune, true);
            ApiEventManager.OnKillUnit.AddListener(this, unit, death =>
            {
                if (buff.SourceUnit is Champion champion)
                {
                    champion.AddGold(champion, death.Unit.Stats.GoldGivenOnDeath.Total);
                }
            });
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
