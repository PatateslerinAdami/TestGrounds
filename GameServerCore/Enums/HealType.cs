namespace GameServerCore.Enums;

public enum HealType : byte
{
    SelfHeal = 0x0,
    HealthRegeneration = 0x1,
    LifeSteal = 0x2,
    SpellVamp = 0x3,
    OutgoingHeal = 0x4,
    IncomingHeal = 0x5,
    Drain = 0x6,
    PhysicalVamp = 0x7
}