using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    internal class PoppyParagonManager : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new() { BuffType = BuffType.COMBAT_ENCHANCER, BuffAddType = BuffAddType.REPLACE_EXISTING, MaxStacks = 1 };
        public StatsModifier StatsModifier { get; private set; } = new();
        public void OnActivate(AttackableUnit u, Buff b, Spell s) { }
        public void OnDeactivate(AttackableUnit u, Buff b, Spell s) { }
        public void OnUpdate(float d) { }
    }

    internal class PoppyParagonIcon : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new() { BuffType = BuffType.COMBAT_ENCHANCER, BuffAddType = BuffAddType.REPLACE_EXISTING, MaxStacks = 1 };
        public StatsModifier StatsModifier { get; private set; } = new();
        public void OnActivate(AttackableUnit u, Buff b, Spell s) { }
        public void OnDeactivate(AttackableUnit u, Buff b, Spell s) { }
        public void OnUpdate(float d) { }
    }

    internal class PoppyParagonParticle : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new() { BuffType = BuffType.COMBAT_ENCHANCER, BuffAddType = BuffAddType.REPLACE_EXISTING, MaxStacks = 1 };
        public StatsModifier StatsModifier { get; private set; } = new();
        public void OnActivate(AttackableUnit u, Buff b, Spell s) { }
        public void OnDeactivate(AttackableUnit u, Buff b, Spell s) { }
        public void OnUpdate(float d) { }
    }
}
