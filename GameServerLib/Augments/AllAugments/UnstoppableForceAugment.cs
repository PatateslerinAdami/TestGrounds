using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.Augments
{
    public class UnstoppableForceAugment : Augment
    {
        public UnstoppableForceAugment() : base(
            "Augment_UnstoppableForce",
            "Unstoppable Force",
            "Gain Cooldown Reduction and Slow Resistance.",
            "")
        {
        }

        public override void Apply(Champion target)
        {
            var mod = new StatsModifier();
            mod.CooldownReduction.FlatBonus = 0.4f;
            mod.SlowResistPercent = 0.5f; 
            target.AddStatModifier(mod);
        }
    }
}