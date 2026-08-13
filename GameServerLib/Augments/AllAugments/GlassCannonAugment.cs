using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.Augments
{
    public class GlassCannonAugment : Augment
    {
        public GlassCannonAugment() : base(
            "aug_glasscannon",
            "Glass Cannon",
            "Gain some AD and AP but lose 500 Health.",
            ""
            )
        { }

        public override void Apply(Champion target)
        {
            var mod = new StatsModifier();
            mod.AttackDamage.FlatBonus = 100.0f;
            mod.AbilityPower.FlatBonus = 100.0f;
            mod.HealthPoints.FlatBonus = -500.0f;
            target.AddStatModifier(mod);
        }
    }
}