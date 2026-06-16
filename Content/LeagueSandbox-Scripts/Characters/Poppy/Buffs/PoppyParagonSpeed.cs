using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    /// <summary>Visible Paragon buff — shows icon + stack count. No stats.</summary>
    internal class PoppyParagonSpeed : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 10
        };
        public StatsModifier StatsModifier { get; private set; } = new();
        public void OnActivate(AttackableUnit u, Buff b, Spell s) { }
        public void OnDeactivate(AttackableUnit u, Buff b, Spell s) { }
        public void OnUpdate(float d) { }
    }

    /// <summary>Internal attack speed buff — only added by W active.</summary>
    internal class PoppyParagonAS : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new()
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            float[] perLevel = { 0.17f, 0.19f, 0.21f, 0.23f, 0.25f };
            int rank = ownerSpell?.CastInfo.SpellLevel ?? 1;
            if (rank < 1 || rank > 5) rank = 1;
            StatsModifier.AttackSpeed.PercentBonus = perLevel[rank - 1];
            unit.AddStatModifier(StatsModifier);
        }
        public void OnDeactivate(AttackableUnit u, Buff b, Spell s) { }
        public void OnUpdate(float d) { }
    }
}
