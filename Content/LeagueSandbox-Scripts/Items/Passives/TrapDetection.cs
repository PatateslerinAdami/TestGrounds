using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace ItemPassives
{
    public abstract class TrapDetectionItem : UniqueStatItem
    {
        private ObjAIBase _owner;
        private float _elapsed;
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            _owner = owner;
        }

        public override void OnUpdate(float diff)
        {
            _elapsed += diff;
            if (_elapsed < 250 || _owner.IsDead)
            {
                return;
            }

            _elapsed %= 250;
            ItemVisionHelpers.Detect(_owner, 1000, true);
        }
    }

    public class ItemID_3187 : TrapDetectionItem
    {
        protected override void Configure()
        {
            StatsModifier.CooldownReduction.FlatBonus = -0.2f;
        }
    }

    public class ItemID_3290 : TrapDetectionItem
    {
        protected override void Configure()
        {
        }
    }
}
