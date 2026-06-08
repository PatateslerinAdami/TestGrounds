using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;

namespace Buffs
{
    // Flow-meter HUD buff
    internal class YasuoPassiveMSCharge : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COUNTER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 100
        };
        public StatsModifier StatsModifier { get; set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
    }
}
