using ItemHelpers;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public abstract class ItemAura : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.AURA,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        protected abstract void Configure();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            Configure();
            unit.AddStatModifier(StatsModifier);
            if (buff.Name == "ZekesHeraldAura")
            {
                NativeItemPresentation.Show(
                    buff,
                    unit == buff.SourceUnit ? "RallyingBannerAuraSelf" : "RallyingBannerAuraFriend",
                    true
                );
            }

            if (buff.Name == "LegionAura")
            {
                NativeItemPresentation.Show(
                    buff,
                    buff.SourceUnit.Inventory.HasItemWithID(3190) ? "IronStylusAura" : unit == buff.SourceUnit ? "ItemAegisoftheLegionAuraSelf" : "ItemAegisoftheLegionAuraFriend",
                    true
                );
            }
        }
    }

    public class AbyssalScepterAura : ItemAura
    {
        public AbyssalScepterAura()
        {
            BuffMetaData.IsHidden = false;
        }

        protected override void Configure()
        {
            StatsModifier.MagicResist.FlatBonus = -20;
        }
    }

    public class ZekesHeraldAura : ItemAura
    {
        protected override void Configure()
        {
            StatsModifier.AttackDamage.FlatBonus = 20;
            StatsModifier.LifeSteal.FlatBonus = 0.1f;
        }
    }

    public class LegionAura : ItemAura
    {
        protected override void Configure()
        {
            StatsModifier.MagicResist.FlatBonus = 20;
            StatsModifier.HealthRegeneration.PercentBaseBonus = 0.75f;
        }
    }
}
