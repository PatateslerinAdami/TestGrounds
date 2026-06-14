using System;
using System.Globalization;
using System.IO;
using System.Text;
using LENet;
using Channel = GameServerCore.Packets.Enums.Channel;

namespace LeagueSandbox.GameServer.Logging
{
    /// <summary>
    /// Captures every outbound packet to a file in the exact same shape as Riot's
    /// <c>.rlp.json</c> replay records — one JSON object per line:
    /// <c>{"Time": &lt;gameTimeMs&gt;, "Bytes": "&lt;base64&gt;", "Channel": n, "Flags": n}</c>.
    /// <para>This lets the same parser used on a reference replay be pointed at our own
    /// output and diffed 1:1 (e.g. to compare Vel'Koz's wire against a Riot replay). The
    /// logged bytes are the plaintext packet body BEFORE Blowfish encryption, which is the
    /// decrypted form the replay tooling sees, so the two are directly comparable.</para>
    /// <para>Off by default. Enable from the packet layer when the env var <c>PACKET_LOG</c>
    /// is set (to a path, or <c>1</c> for the default file). Each LOGICAL packet is logged
    /// exactly once regardless of how many recipients a broadcast fans out to.</para>
    /// </summary>
    public static class PacketLogger
    {
        private static readonly object _lock = new object();
        private static StreamWriter _writer;

        public static bool Enabled { get; private set; }

        /// <summary>
        /// Opens (truncating) the given file and starts capturing. Pass <c>"1"</c> to use the
        /// default file name <c>packetlog.jsonl</c> in the current working directory.
        /// </summary>
        public static void Enable(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (path == "1")
            {
                path = "packetlog.jsonl";
            }

            lock (_lock)
            {
                _writer?.Dispose();
                _writer = new StreamWriter(path, append: false, Encoding.ASCII) { AutoFlush = true };
                Enabled = true;
                Console.WriteLine($"[PacketLogger] capturing outbound packets to {Path.GetFullPath(path)}");
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
        /// Appends one record for an outbound packet. No-op unless capturing is enabled.
        /// </summary>
        public static void Log(byte[] data, Channel channel, PacketFlags flag, float gameTimeMs)
        {
            if (!Enabled || data == null || data.Length == 0)
            {
                return;
            }

            lock (_lock)
            {
                if (_writer == null)
                {
                    return;
                }

                // Match the replay schema field order exactly so the same parser works.
                _writer.Write("{\"Time\": ");
                _writer.Write(gameTimeMs.ToString("R", CultureInfo.InvariantCulture));
                _writer.Write(", \"Bytes\": \"");
                _writer.Write(Convert.ToBase64String(data));
                _writer.Write("\", \"Channel\": ");
                _writer.Write(((int)channel).ToString(CultureInfo.InvariantCulture));
                _writer.Write(", \"Flags\": ");
                _writer.Write(((int)flag).ToString(CultureInfo.InvariantCulture));
                _writer.Write("}\n");
            }
        }
    }
}
