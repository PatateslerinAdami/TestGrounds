using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace LeagueSandbox.GameServer.Augments
{
    public class BrushSweeperAugment : Augment
    {
        public BrushSweeperAugment() : base(
            "Augment_BrushSweeper",
            "Brush Sweeper",
            "Entering a brush grants Movement Speed for 2 seconds.",
            ""
            )
        {
        }

        public override void Apply(Champion target)
        {
            ApiEventManager.OnEnterGrass.AddListener(this, target, OnEnterGrass, false);
        }

        private void OnEnterGrass(AttackableUnit unit)
        {
            var speedMod = new StatsModifier();
            speedMod.MoveSpeed.PercentBonus = 0.6f;
            unit.AddStatModifier(speedMod);

            unit.RegisterTimer(new GameScriptTimer(2.0f, () =>
            {
                unit.RemoveStatModifier(speedMod);
            }));
        }
    }
}