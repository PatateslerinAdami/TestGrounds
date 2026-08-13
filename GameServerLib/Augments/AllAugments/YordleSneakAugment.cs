using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.Augments
{
    public class YordleSneakAugment : Augment
    {
        public YordleSneakAugment() : base(
            "Augment_YordleSneak",
            "Yordle Sneak",
            "Become smaller gain Tenacity and ignore unit collision.",
            "")
        {
        }

        public override void Apply(Champion target)
        {
            var mod = new StatsModifier();
            mod.Size.PercentBonus = -0.3f;
            mod.Tenacity.FlatBonus = 0.3f;
            target.AddStatModifier(mod);

            target.SetStatus(StatusFlags.Ghosted, true);
        }
    }
}