using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.Augments
{
    public class SwiftnessAugment : Augment
    {
        public SwiftnessAugment() : base(
            "aug_swiftness",
            "Gotta Go Fast",
            "Gain +50 Permanent Movement Speed.",
            "")
        { }

        public override void Apply(Champion target)
        {
            var mod = new StatsModifier();
            mod.MoveSpeed.FlatBonus = 50.0f;
            target.AddStatModifier(mod);
        }
    }
}