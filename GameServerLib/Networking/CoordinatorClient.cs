using GameserverControl;
using Google.Protobuf;
using LeagueSandbox.GameServer.Logging;
using log4net;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace LeagueSandbox.GameServer.Networking;

/// <summary>
/// Length-prefixed protobuf client that connects this GameServer to a match
/// coordinator. The wire protocol is defined in
/// <c>Networking/Protobuf/gameserver_control.proto</c>; any coordinator
/// implementation that speaks that proto is compatible regardless of its
/// host language or process model.
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle: construct → <see cref="ConnectAndSendReady"/> after the
/// GameServer's UDP port has bound → <see cref="SendMatchEnded"/> when the
/// match concludes → <see cref="Dispose"/>. The TCP socket stays open
/// between Ready and MatchEnded so the coordinator can detect crashes
/// (unsolicited socket close = abnormal end) and so future Shutdown
/// commands reach the GameServer immediately.
/// </para>
/// <para>
/// Thread-safety: outbound writes are serialized by <see cref="_sendLock"/>,
/// so <see cref="SendMatchEnded"/> and <see cref="SendHeartbeat"/> may be
/// called from any thread. The reader runs on a dedicated background
/// thread and raises <see cref="ShutdownRequested"/> from that thread —
/// subscribers must marshal to whichever thread owns the shutdown logic
/// (typically the game thread).
/// </para>
/// <para>
/// Failure handling: any I/O exception during connect or send is caught,
/// logged, and surfaced via <see cref="ConnectionLost"/>. Coordinator-side
/// problems never abort the GameServer — players who are already connected
/// stay connected and the match plays out normally even if the coordinator
/// disappears.
/// </para>
/// </remarks>
public sealed class CoordinatorClient : IDisposable
{
    private static readonly ILog _logger = LoggerProvider.GetLogger();

    // Defensive cap matching the .proto's documented 1 MiB ceiling. A frame
    // longer than this means the wire is corrupt (or a hostile coordinator);
    // we close the connection rather than allocating gigabytes.
    private const int MAX_FRAME_BYTES = 1024 * 1024;

    private readonly string _host;
    private readonly int    _port;
    private readonly int    _matchId;
    private readonly string _version;

    private TcpClient?     _tcp;
    private NetworkStream? _stream;
    private readonly object _sendLock = new();

    private CancellationTokenSource? _cts;
    private Thread?                  _readerThread;

    private int _disposed;       // Interlocked 0/1 to keep Dispose idempotent
    private int _matchEndedSent; // Interlocked 0/1 — only one MatchEnded per match

    /// <summary>
    /// Raised when the coordinator sends a <c>Shutdown</c> command. The
    /// handler should arrange for the GameServer to send MatchEnded with
    /// reason = SHUTDOWN_REQUESTED and then exit. Fired on the reader
    /// thread; marshal as needed.
    /// </summary>
    public event Action<string>? ShutdownRequested;

    /// <summary>
    /// Raised when the TCP connection to the coordinator drops, either
    /// because the coordinator closed it or because of an I/O error. The
    /// match continues; this is purely informational so the GameServer can
    /// log / suppress further send attempts. Fired on the reader thread.
    /// </summary>
    public event Action<Exception?>? ConnectionLost;

    public CoordinatorClient(string host, int port, int matchId, string version)
    {
        _host    = host    ?? throw new ArgumentNullException(nameof(host));
        _port    = port;
        _matchId = matchId;
        _version = version ?? "";
    }

    /// <summary>
    /// Open the TCP connection and send the initial <c>Ready</c> frame.
    /// Must be called AFTER the GameServer's UDP socket is bound; the
    /// coordinator interprets Ready as "clients can now be routed here".
    /// </summary>
    /// <param name="actualPort">
    /// The UDP port the GameServer actually bound. Echoed in the Ready
    /// message so the coordinator can confirm what to advertise to clients.
    /// </param>
    /// <exception cref="System.Net.Sockets.SocketException">
    /// Thrown if the TCP connect fails. Callers should catch and log —
    /// a coordinator-side failure is not fatal to the GameServer itself.
    /// </exception>
    public void ConnectAndSendReady(int actualPort)
    {
        _tcp = new TcpClient();
        // Disable Nagle: control messages are small and infrequent; latency
        // matters more than packing efficiency, and the coordinator's "wait
        // for Ready" path is held up directly by this send.
        _tcp.NoDelay = true;
        _tcp.Connect(_host, _port);
        _stream = _tcp.GetStream();

        var msg = new GameServerToCoordinator
        {
            Ready = new Ready
            {
                MatchId    = _matchId,
                ActualPort = actualPort,
                Version    = _version
            }
        };
        SendFrame(msg);

        _cts = new CancellationTokenSource();
        _readerThread = new Thread(ReaderLoop)
        {
            IsBackground = true,
            Name         = "CoordinatorReader"
        };
        _readerThread.Start();

        _logger.Info($"[Coordinator] Connected to {_host}:{_port}, sent Ready " +
                     $"(match_id={_matchId}, port={actualPort}).");
    }

    /// <summary>
    /// Send a <c>MatchEnded</c> frame. Idempotent — only the first call
    /// per instance actually sends; subsequent calls are no-ops, so the
    /// GameServer can call this from multiple match-end paths without
    /// worrying about double-sends.
    /// </summary>
    public void SendMatchEnded(MatchEnded.Types.Reason reason,
                               int durationSeconds,
                               int winningTeam = 0,
                               string detail   = "")
    {
        if (Interlocked.Exchange(ref _matchEndedSent, 1) != 0)
            return;

        var msg = new GameServerToCoordinator
        {
            MatchEnded = new MatchEnded
            {
                MatchId         = _matchId,
                Reason          = reason,
                DurationSeconds = durationSeconds,
                WinningTeam     = winningTeam,
                Detail          = detail ?? ""
            }
        };

        try
        {
            SendFrame(msg);
            _logger.Info($"[Coordinator] Sent MatchEnded (reason={reason}, " +
                         $"duration={durationSeconds}s, winner={winningTeam}).");
        }
        catch (Exception e)
        {
            _logger.Warn($"[Coordinator] MatchEnded send failed: {e.Message}");
        }
    }

    /// <summary>
    /// Send an optional liveness heartbeat. Coordinators that don't track
    /// these will ignore them; coordinators that do can use them to drive
    /// a watchdog independent of TCP-level keepalives.
    /// </summary>
    public void SendHeartbeat(int connectedPlayers, int elapsedSeconds)
    {
        var msg = new GameServerToCoordinator
        {
            Heartbeat = new Heartbeat
            {
                MatchId          = _matchId,
                ConnectedPlayers = connectedPlayers,
                ElapsedSeconds   = elapsedSeconds
            }
        };
        try
        {
            SendFrame(msg);
        }
        catch (Exception)
        {
            // Heartbeats are advisory — swallow failures rather than
            // logging on every tick. ConnectionLost will fire from the
            // reader thread once the socket is genuinely dead.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try { _cts?.Cancel(); } catch { /* best-effort */ }
        try { _stream?.Close(); } catch { /* best-effort */ }
        try { _tcp?.Close(); } catch { /* best-effort */ }

        // 2s join is generous: the reader is parked in a blocking Read,
        // which our socket close above unblocks immediately on every
        // platform we care about.
        try { _readerThread?.Join(TimeSpan.FromSeconds(2)); } catch { }

        _cts?.Dispose();
    }

    // ── internal ─────────────────────────────────────────────────────────

    private void SendFrame(GameServerToCoordinator msg)
    {
        if (_stream == null)
            throw new InvalidOperationException(
                "CoordinatorClient.ConnectAndSendReady must be called before sending other messages.");

        var body  = msg.ToByteArray();
        var len   = body.Length;

        // 4-byte big-endian length prefix.
        var frame = new byte[4 + len];
        frame[0] = (byte)((len >> 24) & 0xff);
        frame[1] = (byte)((len >> 16) & 0xff);
        frame[2] = (byte)((len >>  8) & 0xff);
        frame[3] = (byte)( len        & 0xff);
        Buffer.BlockCopy(body, 0, frame, 4, len);

        lock (_sendLock)
        {
            _stream.Write(frame, 0, frame.Length);
            _stream.Flush();
        }
    }

    private void ReaderLoop()
    {
        try
        {
            while (_cts is { IsCancellationRequested: false } &&
                   _stream is not null)
            {
                var msg = ReadFrame(_stream);
                if (msg == null)
                    break; // EOF

                using var _frameScope = Profiler.Scope($"CoordinatorFrame:{msg.PayloadCase}", "network");
                switch (msg.PayloadCase)
                {
                    case CoordinatorToGameServer.PayloadOneofCase.Shutdown:
                        var reason = msg.Shutdown.Reason ?? "";
                        _logger.Info($"[Coordinator] Shutdown requested: {reason}");
                        try { ShutdownRequested?.Invoke(reason); } catch { /* swallow handler errors */ }
                        break;

                    case CoordinatorToGameServer.PayloadOneofCase.None:
                    default:
                        // Forward-compat: unknown oneof cases are ignored
                        // rather than treated as errors. A future coordinator
                        // may extend the schema and we don't want to churn
                        // every GameServer to recognize new commands.
                        break;
                }
            }

            try { ConnectionLost?.Invoke(null); } catch { }
        }
        catch (Exception e)
        {
            // Don't log if we're shutting down on purpose — Dispose closes
            // the socket, which throws here, which is expected.
            if (Volatile.Read(ref _disposed) == 0)
                _logger.Info($"[Coordinator] Reader exiting: {e.Message}");
            try { ConnectionLost?.Invoke(e); } catch { }
        }
    }

    private static CoordinatorToGameServer? ReadFrame(NetworkStream stream)
    {
        var lenBuf = new byte[4];
        if (!ReadExact(stream, lenBuf, 0, 4))
            return null;

        int len = (lenBuf[0] << 24) | (lenBuf[1] << 16) |
                  (lenBuf[2] <<  8) |  lenBuf[3];

        if (len <= 0 || len > MAX_FRAME_BYTES)
            throw new IOException($"Invalid frame length from coordinator: {len}");

        var body = new byte[len];
        if (!ReadExact(stream, body, 0, len))
            return null;

        return CoordinatorToGameServer.Parser.ParseFrom(body);
    }

    private static bool ReadExact(NetworkStream stream, byte[] dst, int off, int len)
    {
        int got = 0;
        while (got < len)
        {
            int n = stream.Read(dst, off + got, len - got);
            if (n <= 0)
                return false;
            got += n;
        }
        return true;
    }
}
