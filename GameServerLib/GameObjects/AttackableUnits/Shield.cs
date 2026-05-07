using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace GameServerLib.GameObjects.AttackableUnits
{
    public class Shield
    {
        public Shield(ObjAIBase sourceUnit, AttackableUnit targetUnit, bool physical, bool magical, float amount)
        {
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            Physical = physical;
            Magical = magical;
            Amount = amount;
        }

        public ObjAIBase SourceUnit { get; }
        public AttackableUnit TargetUnit { get; }
        public bool Physical { get; }
        public bool Magical { get; }
        public float Amount { get; protected set; }

        public float Consume(DamageData damageData)
        {
            float consumed = 0;
            if ((damageData.DamageType != DamageType.DAMAGE_TYPE_PHYSICAL || !Physical) &&
                (damageData.DamageType != DamageType.DAMAGE_TYPE_MAGICAL || !Magical) &&
                damageData.DamageType != DamageType.DAMAGE_TYPE_MIXED)
            {
                return consumed;
            }

            if (Amount > damageData.PostMitigationDamage)
            {
                Amount -= damageData.PostMitigationDamage;
                consumed = damageData.PostMitigationDamage;
                damageData.PostMitigationDamage = 0;
            }
            else
            {
                damageData.PostMitigationDamage -= Amount;
                consumed = Amount;
                Amount = 0;
            }

            return consumed;
        }

        public bool IsConsumed()
        {
            return Amount <= 0;
        }

        // Reduces Amount by the given value, clamped at 0. Returns
        // the actual delta applied so the caller can mirror it in packets.
        public float Reduce(float amount)
        {
            if (amount <= 0)
            {
                return 0;
            }
            float applied = amount > Amount ? Amount : amount;
            Amount -= applied;
            return applied;
        }

        // Increases Amount by the given value. Returns the delta
        // applied so the caller can mirror it in packets.
        public float Increase(float amount)
        {
            if (amount <= 0)
            {
                return 0;
            }
            Amount += amount;
            return amount;
        }
    }
}
