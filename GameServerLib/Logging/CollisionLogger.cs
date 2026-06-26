using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace LeagueSandbox.GameServer.Logging
{
    /// <summary>
    /// Diagnostic, env-gated logger for the server-side collision response (the divergence source
    /// behind the client-side "minion teleport" snaps). Off by default; enable by setting the env
    /// var <c>COLLISION_LOG</c> to a path (or <c>1</c> for <c>collisionlog.jsonl</c>).
    /// <para>One JSON object per line:
    /// <c>{"t": gameTimeMs, "net": netId, "ev": "group|avoid|stuck|resync", "mag": pushMagnitude,
    /// "drift": unreplicatedDriftMagnitude, "x": posX, "z": posZ}</c>.</para>
    /// <para><c>mag</c> on a <c>group</c>/<c>avoid</c>/<c>stuck</c> line = how far THIS tick's collision
    /// shoved the unit off its intended walk (world units). <c>drift</c> on a <c>resync</c> line = the
    /// accumulated server-vs-last-replicated divergence at the moment we re-broadcast — i.e. the size
    /// of the hard-snap the client receives. Summing/percentiling these answers "how big is the
    /// collision mismatch the client has to absorb".</para>
    /// </summary>
    public static class CollisionLogger
    {
        private static readonly object _lock = new object();
        private static StreamWriter _writer;

        public static bool Enabled { get; private set; }

        public static void Enable(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (path == "1")
            {
                path = "collisionlog.jsonl";
            }

            lock (_lock)
            {
                _writer?.Dispose();
                _writer = new StreamWriter(path, append: false, Encoding.ASCII) { AutoFlush = true };
                Enabled = true;
                Console.WriteLine($"[CollisionLogger] capturing collision pushes to {Path.GetFullPath(path)}");
            }
        }

        public static void Disable()
        {
            lock (_lock)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
                Enabled = false;
            }
        }

        /// <summary>
        /// Appends one collision event. No-op unless capturing is enabled. <paramref name="mag"/> is
        /// the per-tick push magnitude (group/avoid/stuck events); <paramref name="drift"/> is the
        /// accumulated unreplicated drift (resync events). Pass 0 for whichever doesn't apply.
        /// </summary>
        public static void Log(float gameTimeMs, uint netId, string ev, float mag, float drift, Vector2 pos)
        {
            if (!Enabled)
            {
                return;
            }

            lock (_lock)
            {
                if (_writer == null)
                {
                    return;
                }

                var ci = CultureInfo.InvariantCulture;
                _writer.Write("{\"t\": ");
                _writer.Write(gameTimeMs.ToString("R", ci));
                _writer.Write(", \"net\": ");
                _writer.Write(netId.ToString(ci));
                _writer.Write(", \"ev\": \"");
                _writer.Write(ev);
                _writer.Write("\", \"mag\": ");
                _writer.Write(mag.ToString("F2", ci));
                _writer.Write(", \"drift\": ");
                _writer.Write(drift.ToString("F2", ci));
                _writer.Write(", \"x\": ");
                _writer.Write(pos.X.ToString("F1", ci));
                _writer.Write(", \"z\": ");
                _writer.Write(pos.Y.ToString("F1", ci));
                _writer.Write("}\n");
            }
        }
    }
}
