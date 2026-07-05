using System;
using System.Collections.Generic;
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
        private static readonly List<uint> _championTags = new List<uint>();

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

                foreach (var netId in _championTags)
                {
                    WriteChampionTag(netId);
                }
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
        /// Marks a NetID as a champion in the capture. Riot replays identify champions by the
        /// sender of S2C_HeroStats (0x46), a packet we never send — so replay tooling
        /// (tools/wpath.py) found no champions in our captures and skipped all champion metrics.
        /// This writes a capture-only synthetic record whose bytes are just the 5-byte header
        /// <c>[0x46][netId u32]</c>; it never goes on the wire. Safe to call before Enable
        /// (tags are buffered and flushed when capturing starts).
        /// </summary>
        public static void TagChampion(uint netId)
        {
            lock (_lock)
            {
                if (_championTags.Contains(netId))
                {
                    return;
                }
                _championTags.Add(netId);
                if (Enabled && _writer != null)
                {
                    WriteChampionTag(netId);
                }
            }
        }

        private static void WriteChampionTag(uint netId)
        {
            var bytes = new byte[5];
            bytes[0] = 0x46;
            bytes[1] = (byte)netId;
            bytes[2] = (byte)(netId >> 8);
            bytes[3] = (byte)(netId >> 16);
            bytes[4] = (byte)(netId >> 24);
            _writer.Write("{\"Time\": 0, \"Bytes\": \"");
            _writer.Write(Convert.ToBase64String(bytes));
            _writer.Write("\", \"Channel\": 0, \"Flags\": 0}\n");
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
