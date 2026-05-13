namespace LeagueSandbox.GameServer.Networking;

/// <summary>
/// Wire-up parameters for the optional <see cref="CoordinatorClient"/>.
/// Populated from the top-level <c>coordinatorChannel</c> object in the
/// GameInfo.json the coordinator writes at spawn time (parsed in
/// <c>Config.LoadConfig</c>). See <c>Networking/README.md</c> for the
/// JSON-side contract and <c>Networking/Protobuf/gameserver_control.proto</c>
/// for the on-wire protocol.
/// </summary>
/// <remarks>
/// If the <c>coordinatorChannel</c> object is absent from the JSON the
/// GameServer runs in legacy/standalone mode and the coordinator client
/// is never constructed — consistent with how the GameServer ran before
/// this protocol existed.
/// </remarks>
public sealed record CoordinatorConfig(string Host, int Port, int MatchId)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Host) && Port > 0 && Port <= 65535;
}
