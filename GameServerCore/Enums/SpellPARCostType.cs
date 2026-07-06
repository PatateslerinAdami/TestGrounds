namespace GameServerCore.Enums
{
    /// <summary>
    /// CostType byte of S2C_UnitSetSpellPARCost. Decides how the client applies the override to a
    /// spell slot's Primary Ability Resource (mana) cost (decomp GameClient.cpp:5036).
    /// </summary>
    public enum SpellPARCostType : byte
    {
        // SpellDataInst::SetIncManaCost(amount) — flat additive increment on the slot's mana cost
        // (e.g. Kog'Maw's Living Artillery: positive, escalating per cast).
        Additive = 0,
        // SpellDataInst::SetIncMultiplicativeManaCost(amount) — multiplicative increment; used by the
        // recast-window ults observed on the wire (Ahri R / Kha'Zix R set this negative on cast, 0 on restore).
        Multiplicative = 1,
    }
}
