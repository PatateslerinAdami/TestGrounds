using System.Linq;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemHelpers
{
    internal static class ItemCleanseHelpers
    {
        public static void Cleanse(AttackableUnit unit, bool allDebuffs)
        {
            var types = new[]
            {
                BuffType.STUN,
                BuffType.SNARE,
                BuffType.TAUNT,
                BuffType.FEAR,
                BuffType.SILENCE,
                BuffType.SLOW
            };
            foreach (var buff in unit.GetBuffs().ToArray())
            {
                if (types.Contains(buff.BuffType) || (allDebuffs && buff.SourceUnit != null
                    && buff.SourceUnit.Team != unit.Team
                    && buff.BuffType != BuffType.AURA))
                {
                    RemoveBuff(buff);
                }
            }
        }
    }
}
