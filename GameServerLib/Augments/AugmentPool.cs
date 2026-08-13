using LeagueSandbox.GameServer.Augments;
using System.Collections.Generic;

namespace LeagueSandbox.GameServer.Augments
{
    public static class AugmentPool
    {
        private static readonly Dictionary<string, List<Augment>> _pools = new Dictionary<string, List<Augment>>();

        public static void Initialize()
        {
            _pools["Base"] = new List<Augment>();
            _pools["Hider"] = new List<Augment>();
            _pools["Seeker"] = new List<Augment>();

            _pools["Base"].Add(new SwiftnessAugment());
            _pools["Base"].Add(new GlassCannonAugment());

            _pools["Hider"].Add(new BrushStalkerAugment());
            _pools["Hider"].Add(new PanicButtonAugment());
            _pools["Hider"].Add(new YordleSneakAugment());
            _pools["Hider"].Add(new SmokeBombAugment());

            _pools["Seeker"].Add(new PredatorsMarkAugment());
            _pools["Seeker"].Add(new BrushSweeperAugment());
            _pools["Seeker"].Add(new UnstoppableForceAugment());
        }

        public static List<Augment> GetAugmentsFromPools(params string[] poolNames)
        {
            var result = new List<Augment>();
            foreach (var poolName in poolNames)
            {
                if (_pools.TryGetValue(poolName, out var augments))
                {
                    result.AddRange(augments);
                }
            }
            return result;
        }
    }
}