namespace Buffs
{
    public class AatroxWPower : AatroxWLife
    {
        protected override string Effect
        {
            get
            {
                return "W_WeaponPower";
            }
        }
    }

    public class AatroxWONHPowerBuff : AatroxWONHLifeBuff
    {
    }
}
