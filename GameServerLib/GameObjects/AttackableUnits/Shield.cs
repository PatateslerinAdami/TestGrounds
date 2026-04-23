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
    }
}
