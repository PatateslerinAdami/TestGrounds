using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace GameServerLib.GameObjects.AttackableUnits;

public class HealData {
    /// <summary>
    ///     Unit that granted healing.
    /// </summary>
    public AttackableUnit Healer { get; set; }

    /// <summary>
    ///     The initial heal amount before any event modifications.
    /// </summary>
    public float OriginalHealAmount { get; set; }

    /// <summary>
    ///     Mutable heal amount used by heal modifiers (before overheal clamp).
    /// </summary>
    public float HealAmount { get; set; }

    /// <summary>
    ///     Final amount of health restored after all modifications and clamping.
    /// </summary>
    public float PostModificationHealAmount { get; set; }

    /// <summary>
    ///     Type of heal received.
    /// </summary>
    public HealType HealType { get; set; }

    /// <summary>
    ///     Unit that will receive healing.
    /// </summary>
    public AttackableUnit Target { get; set; }
}