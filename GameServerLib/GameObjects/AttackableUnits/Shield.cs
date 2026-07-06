using System;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace GameServerLib.GameObjects.AttackableUnits
{
    public class Shield
    {
        // The buff that owns this shield, if any. Shields created by a buff script pass the buff
        // so the engine can read Riot's OnPreDamagePriority / DoOnPreDamageInExpirationOrder off
        // its metadata and its remaining lifetime — these drive the consumption order in
        // AttackableUnit.ConsumeShields. Item procs and buff-less shields leave it null.
        private readonly Buff _owner;

        public Shield(ObjAIBase sourceUnit, AttackableUnit targetUnit, bool physical, bool magical, float amount, Buff owner = null)
        {
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            Physical = physical;
            Magical = magical;
            Amount = amount;
            _owner = owner;
        }

        public ObjAIBase SourceUnit { get; }
        public AttackableUnit TargetUnit { get; }
        public bool Physical { get; }
        public bool Magical { get; }
        public float Amount { get; protected set; }

        // Riot scriptBaseBuff::OnPreDamagePriority — higher priority shields consume incoming
        // damage first. Riot's default is -1; buff-less shields here default to 0, which only
        // affects the relative order among a unit's shields (the only observable effect).
        public int Priority => _owner?.BuffScript?.BuffMetaData?.OnPreDamagePriority ?? 0;

        // Riot DoOnPreDamageInExpirationOrder (4.20-new): when set, this shield is consumed
        // before same-priority shields that expire later — spend the one about to vanish first.
        public bool ConsumeInExpirationOrder => _owner?.BuffScript?.BuffMetaData?.DoOnPreDamageInExpirationOrder ?? false;

        // Remaining lifetime of the owning buff, used as the expiration-order sort key.
        // Infinite and buff-less shields sort last (never "about to expire").
        public float RemainingTime =>
            _owner == null || _owner.IsBuffInfinite()
                ? float.MaxValue
                : Math.Max(0f, _owner.Duration - _owner.TimeElapsed);

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
