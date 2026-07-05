using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace LeagueSandbox.GameServer.Logging
{
    /// <summary>
    /// Diagnostic, env-gated logger for ISSUED minion paths (the WaypointGroup the client receives
    /// and hard-snaps to). Off by default; enable with env var <c>PATH_LOG</c> (a path, or <c>1</c>
    /// for <c>pathlog.jsonl</c>). Logged ONLY when a path actually broadcasts (the same condition the
    /// wire sees), so it lines up 1:1 with what <c>tools/minionroute.py</c> extracts from a Riot
    /// replay — letting us diff our routing geometry against Riot's directly.
    /// <para>One JSON object per line:
    /// <c>{"t":ms,"net":id,"reason":"fwd:target|fwd:pathend|attack|reroute|other","n":count,
    /// "chord":wp0-wpLast,"look":wp0-apex,"depth":maxPerpFromChord,"clr":minPathClearanceToAllyBody,
    /// "bodyd":nearestAllyBodyDistToChord}</c>.</para>
    /// <para>Compare against Riot (minionroute.py on 343e3502): chord median ~277u, look median ~110u,
    /// depth median ~25u, and a passed body ends ~33u from the WALKED line (clr). Our wire showed
    /// clr ~5u (we route THROUGH bodies and separate via the per-tick collision push instead) and
    /// chord/look ~2x Riot (long, early-planned, stale paths). This log localises WHICH issue site
    /// produces those.</para>
    /// </summary>
    public static class PathLogger
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
                path = "pathlog.jsonl";
            }

            lock (_lock)
            {
                _writer?.Dispose();
                _writer = new StreamWriter(path, append: false, Encoding.ASCII) { AutoFlush = true };
                Enabled = true;
                Console.WriteLine($"[PathLogger] capturing issued minion paths to {Path.GetFullPath(path)}");
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

        private static float PerpToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.LengthSquared();
            if (l2 < 1e-6f) return Vector2.Distance(p, a);
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float PerpToPolyline(Vector2 p, IReadOnlyList<Vector2> pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float d = PerpToSeg(p, pts[i], pts[i + 1]);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Logs one forward-nav DECISION (every SetStateAndMoveToForwardNav tick for a lane minion),
        /// so the cap transition (navIndex reaching maxIndex → target = turret, then advancing past a
        /// dead/passed turret) can be reconstructed as a per-minion timeline. <paramref name="action"/>
        /// = "stop" | "issue:&lt;reason&gt;" | "hold" (no re-issue this tick). Correlate with PACKET_LOG
        /// 0x61 wp0 to see the position jump. No-op unless enabled.
        /// </summary>
        public static void LogNav(float gameTimeMs, uint netId, int navIndex, int maxIndex, bool capped,
            float distToTarget, float posX, float posZ, string action)
        {
            if (!Enabled) return;
            lock (_lock)
            {
                if (_writer == null) return;
                var ci = CultureInfo.InvariantCulture;
                _writer.Write("{\"t\": ");
                _writer.Write(gameTimeMs.ToString("R", ci));
                _writer.Write(", \"net\": ");
                _writer.Write(netId.ToString(ci));
                _writer.Write(", \"ev\": \"nav\", \"navIdx\": ");
                _writer.Write(navIndex.ToString(ci));
                _writer.Write(", \"maxIdx\": ");
                _writer.Write(maxIndex.ToString(ci));
                _writer.Write(", \"capped\": ");
                _writer.Write(capped ? "true" : "false");
                _writer.Write(", \"dtarget\": ");
                _writer.Write(distToTarget.ToString("F1", ci));
                _writer.Write(", \"x\": ");
                _writer.Write(posX.ToString("F1", ci));
                _writer.Write(", \"z\": ");
                _writer.Write(posZ.ToString("F1", ci));
                _writer.Write(", \"action\": \"");
                _writer.Write(action);
                _writer.Write("\"}\n");
            }
        }

        /// <summary>
        /// Logs a lane-minion TARGETING event (acquire / drop) so the attack↔forward oscillation near
        /// a turret can be diagnosed: which unit is targeted (<paramref name="targetType"/> +
        /// <paramref name="targetNet"/>), whether it is reachable (<paramref name="reachable"/> =
        /// CheckIsGetToAble), how far, and what <paramref name="action"/> drove it
        /// ("acquire" | "antikite-drop" | "pathblocked-drop" | "lost-drop"). No-op unless enabled.
        /// </summary>
        public static void LogTarget(float gameTimeMs, uint netId, uint targetNet, string targetType,
            bool reachable, float distToTarget, float posX, float posZ, string action)
        {
            if (!Enabled) return;
            lock (_lock)
            {
                if (_writer == null) return;
                var ci = CultureInfo.InvariantCulture;
                _writer.Write("{\"t\": ");
                _writer.Write(gameTimeMs.ToString("R", ci));
                _writer.Write(", \"net\": ");
                _writer.Write(netId.ToString(ci));
                _writer.Write(", \"ev\": \"target\", \"tnet\": ");
                _writer.Write(targetNet.ToString(ci));
                _writer.Write(", \"ttype\": \"");
                _writer.Write(targetType ?? "none");
                _writer.Write("\", \"reach\": ");
                _writer.Write(reachable ? "true" : "false");
                _writer.Write(", \"dtarget\": ");
                _writer.Write(distToTarget.ToString("F1", ci));
                _writer.Write(", \"x\": ");
                _writer.Write(posX.ToString("F1", ci));
                _writer.Write(", \"z\": ");
                _writer.Write(posZ.ToString("F1", ci));
                _writer.Write(", \"action\": \"");
                _writer.Write(action);
                _writer.Write("\"}\n");
            }
        }

        /// <summary>
        /// Logs one client-vs-server position sample (ev:"desync"). Source: every client move order
        /// (NPC_IssueOrderReq) carries the client's locally built path whose Waypoint[0] is the
        /// position the CLIENT believes it is at — the distance to our authoritative server position
        /// at receive time is a direct measurement of client/server movement divergence.
        /// <para>Caveats for analysis: the client waypoint is quantized to the 2u wire grid, and the
        /// sample includes ~one-way network latency of genuine transit (the client clicked slightly
        /// in the past, the server has walked on since) — on localhost that term is negligible, so
        /// d ≈ true desync. Only emitted while a player clicks, i.e. it samples exactly the moments
        /// that FEEL laggy/snappy when divergence is high.</para>
        /// </summary>
        public static void LogDesync(float gameTimeMs, uint netId, float clientX, float clientZ,
            float serverX, float serverZ, bool moving, string orderType)
        {
            if (!Enabled) return;
            float dx = clientX - serverX, dz = clientZ - serverZ;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            lock (_lock)
            {
                if (_writer == null) return;
                var ci = CultureInfo.InvariantCulture;
                _writer.Write("{\"t\": ");
                _writer.Write(gameTimeMs.ToString("R", ci));
                _writer.Write(", \"net\": ");
                _writer.Write(netId.ToString(ci));
                _writer.Write(", \"ev\": \"desync\", \"d\": ");
                _writer.Write(d.ToString("F1", ci));
                _writer.Write(", \"cx\": ");
                _writer.Write(clientX.ToString("F1", ci));
                _writer.Write(", \"cz\": ");
                _writer.Write(clientZ.ToString("F1", ci));
                _writer.Write(", \"sx\": ");
                _writer.Write(serverX.ToString("F1", ci));
                _writer.Write(", \"sz\": ");
                _writer.Write(serverZ.ToString("F1", ci));
                _writer.Write(", \"moving\": ");
                _writer.Write(moving ? "true" : "false");
                _writer.Write(", \"order\": \"");
                _writer.Write(orderType);
                _writer.Write("\"}\n");
            }
        }

        /// <summary>
        /// Logs one client movement-ack (ev:"ack"). The client echoes the SyncID of the last
        /// movement packet it APPLIED (PKT_C2S_MoveConfirm — Riot's server ignores it, we only log).
        /// Our WireSyncID is a monotonic session clock at 2/3 per ms, so
        /// <c>(nowSync − ackedSync) · 1.5</c> ≈ milliseconds between us building that movement
        /// packet and the client's ack arriving back ≈ RTT + client apply delay ("latms"). A rising
        /// latms series = growing client-side lag; gaps in acked syncIDs = movement packets the
        /// client dropped as stale (CanSyncUpdate gate).
        /// </summary>
        public static void LogMoveAck(float gameTimeMs, uint netId, int ackedSync, int nowSync, byte teleportCount)
        {
            if (!Enabled) return;
            lock (_lock)
            {
                if (_writer == null) return;
                var ci = CultureInfo.InvariantCulture;
                _writer.Write("{\"t\": ");
                _writer.Write(gameTimeMs.ToString("R", ci));
                _writer.Write(", \"net\": ");
                _writer.Write(netId.ToString(ci));
                _writer.Write(", \"ev\": \"ack\", \"sync\": ");
                _writer.Write(ackedSync.ToString(ci));
                _writer.Write(", \"latms\": ");
                _writer.Write(((nowSync - ackedSync) * 1.5f).ToString("F1", ci));
                _writer.Write(", \"tele\": ");
                _writer.Write(teleportCount.ToString(ci));
                _writer.Write("}\n");
            }
        }

        /// <summary>
        /// Logs one issued path. <paramref name="path"/> is the just-set waypoint list (index 0 =
        /// current position). <paramref name="allyBodies"/> = nearby same-team body positions (for
        /// the clearance metric — pass the live collision-neighbour positions). No-op unless enabled.
        /// </summary>
        public static void Log(float gameTimeMs, uint netId, string reason,
            IReadOnlyList<Vector2> path, IReadOnlyList<Vector2> allyBodies)
        {
            if (!Enabled || path == null || path.Count < 2)
            {
                return;
            }

            Vector2 a = path[0];
            Vector2 b = path[path.Count - 1];
            float chord = Vector2.Distance(a, b);

            // Detour depth + apex (max perpendicular deviation of an intermediate waypoint).
            float depth = 0f;
            Vector2 apex = b;
            for (int i = 1; i < path.Count - 1; i++)
            {
                float d = PerpToSeg(path[i], a, b);
                if (d > depth) { depth = d; apex = path[i]; }
            }
            float look = Vector2.Distance(a, apex);

            // Clearance: nearest ally body to the chord (between endpoints) + its distance to the
            // WALKED polyline. Mirrors minionroute.py's body-route clearance metric.
            float clr = -1f, bodyd = -1f;
            if (allyBodies != null && allyBodies.Count > 0)
            {
                Vector2 ab = b - a;
                float l2 = ab.LengthSquared();
                float bestChord = float.MaxValue;
                Vector2 nearest = default;
                bool found = false;
                for (int i = 0; i < allyBodies.Count; i++)
                {
                    Vector2 o = allyBodies[i];
                    float tt = l2 > 1e-6f ? Vector2.Dot(o - a, ab) / l2 : -1f;
                    if (tt <= 0.05f || tt >= 0.95f) continue; // only bodies between the endpoints
                    float d = PerpToSeg(o, a, b);
                    if (d < bestChord) { bestChord = d; nearest = o; found = true; }
                }
                if (found)
                {
                    bodyd = bestChord;
                    clr = PerpToPolyline(nearest, path);
                }
            }

            lock (_lock)
            {
                if (_writer == null) return;
                var ci = CultureInfo.InvariantCulture;
                _writer.Write("{\"t\": ");
                _writer.Write(gameTimeMs.ToString("R", ci));
                _writer.Write(", \"net\": ");
                _writer.Write(netId.ToString(ci));
                _writer.Write(", \"reason\": \"");
                _writer.Write(reason ?? "other");
                _writer.Write("\", \"n\": ");
                _writer.Write(path.Count.ToString(ci));
                _writer.Write(", \"chord\": ");
                _writer.Write(chord.ToString("F1", ci));
                _writer.Write(", \"look\": ");
                _writer.Write(look.ToString("F1", ci));
                _writer.Write(", \"depth\": ");
                _writer.Write(depth.ToString("F1", ci));
                _writer.Write(", \"clr\": ");
                _writer.Write(clr.ToString("F1", ci));
                _writer.Write(", \"bodyd\": ");
                _writer.Write(bodyd.ToString("F1", ci));
                _writer.Write("}\n");
            }
        }
    }
}
