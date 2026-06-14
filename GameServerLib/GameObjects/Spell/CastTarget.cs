using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.GameObjects.SpellNS
{
    public class CastTarget
    {
        public AttackableUnit Unit { get; protected set; }
        public HitResult HitResult { get; protected set; }

        public CastTarget(AttackableUnit unit, HitResult hitResult)
        {
            Unit = unit;
            HitResult = hitResult;
        }

        public static HitResult GetHitResult(AttackableUnit unit, bool isAutoAttack, bool isNextAutoCrit, bool isNextAutoMiss = false, bool isNextAutoDodged = false)
        {
            if (isAutoAttack)
            {
                // Miss takes precedence over crit: a blinded "crit" still misses.
                if (isNextAutoMiss)
                {
                    return HitResult.HIT_Miss;
                }
                // Dodge (target evaded) — like miss, suppresses the attack; takes precedence over crit.
                if (isNextAutoDodged)
                {
                    return HitResult.HIT_Dodge;
                }
                if (isNextAutoCrit && unit is not LaneTurret)
                {
                    return HitResult.HIT_Critical;
                }
            }
            return HitResult.HIT_Normal;
        }
    }
}