using System.Linq;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemHelpers
{
    internal static class ItemVisionHelpers
    {
        private static readonly string[] TrapModels =
        {
            "TeemoMushroom",
            "ShacoBox",
            "CaitlynTrap",
            "Nidalee_Spear"
        };
        public static void Reveal(ObjAIBase owner, AttackableUnit target, float duration)
        {
            target.RevealSpecificUnit(duration, persistsThroughStealth: true);
            AddUnitPerceptionBubble(target, 1, duration, owner.Team, revealSpecificUnitOnly: target, ignoresLoS: true);
        }

        public static void Detect(ObjAIBase owner, float radius, bool trapsOnly)
        {
            foreach (var unit in EnumerateUnitsInRange(owner.Position, radius, true))
            {
                if (unit.Team == owner.Team || (trapsOnly && !TrapModels.Contains(unit.Model)))
                {
                    continue;
                }

                Reveal(owner, unit, 0.3f);
            }
        }
    }
}
