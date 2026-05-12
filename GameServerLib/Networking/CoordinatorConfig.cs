namespace LeagueSandbox.GameServer.Networking;

/// <summary>
/// Wire-up parameters for the optional <see cref="CoordinatorClient"/>.
/// Populated from the GameServer's <c>--coord-host</c>, <c>--coord-port</c>,
/// and <c>--match-id</c> CLI args (see <c>gameserver_control.proto</c>).
/// </summary>
/// <remarks>
/// All three values must be non-default for the channel to activate. If any
/// is missing the GameServer runs in legacy/standalone mode and the
/// coordinator client is never constructed — consistent with how the
/// GameServer ran before this protocol existed.
/// </remarks>
public sealed record CoordinatorConfig(string Host, int Port, int MatchId)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Host) && Port > 0 && Port <= 65535;
}
