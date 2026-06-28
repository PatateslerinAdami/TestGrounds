namespace GameServerCore.Enums
{
    /// <summary>
    /// Lifecycle status of a unit's current order, mirroring Riot's <c>order_status_e</c>
    /// (AIEnums.h) tracked by <c>IssueOrders::savedOrderStatus</c>. The faithful 4.20 order pipeline is
    /// <c>HandleNewOrder</c> (issue → PENDING) → <c>TryToExecuteOrder</c> ? <c>ExecuteOrder</c> (EXECUTED)
    /// : <c>PostponeOrder</c> (POSTPONED) → per-tick <c>RouteOrder</c> retry until executable.
    /// See docs/ISSUE_ORDERS_STATE_MACHINE_PLAN.md (S2).
    /// </summary>
    public enum OrderState
    {
        Clear = 0,      // ORDER_STATUS_CLEAR — no order
        Pending = 1,    // ORDER_STATUS_PENDING — issued, awaiting/attempting execution
        Postponed = 2,  // ORDER_STATUS_POSTPONED — not executable yet (e.g. cast out of range); retried each tick
        Executed = 3    // ORDER_STATUS_EXECUTED — performed
    }
}
