using System;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Spells
{
    internal static class AatroxHelpers
    {
        internal static string Effect(ObjAIBase owner, string suffix)
        {
            return (owner.SkinID is 1 or 2 ? $"Aatrox_Skin0{owner.SkinID}_" : "Aatrox_Base_") + suffix;
        }

        internal static void PayHealth(ObjAIBase owner, float amount)
        {
            float paid = Math.Clamp(amount, 0, Math.Max(0, owner.Stats.CurrentHealth - 1));
            owner.Stats.CurrentHealth -= paid;
            if (owner.GetBuffWithName("AatroxPassive") is { Remove: false, BuffScript: Buffs.AatroxPassive passive })
            {
                passive.AddBlood(paid);
            }
        }
    }
}
