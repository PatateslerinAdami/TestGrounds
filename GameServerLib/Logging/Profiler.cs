using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace LeagueSandbox.GameServer.Logging
{
    /// <summary>
    /// CPU profiler that records named scopes into a Perfetto-compatible
    /// Chrome Trace Event JSON file. Drag the resulting profile_*.json into
    /// https://ui.perfetto.dev to view a flame chart of game-loop work.
    ///
    /// Disabled by default. When disabled, Scope() returns a default struct
    /// whose Dispose() is a no-op — cost reduces to one boolean check per
    /// scope, with no allocation.
    ///
    /// Usage:
    ///     using (Profiler.Scope("ObjectManager.Update"))
    ///     {
    ///         // ... work ...
    ///     }
    /// </summary>
    public static class Profiler
    {
        public static bool Enabled { get; private set; }

        private static readonly ConcurrentDictionary<int, ThreadBuffer> _buffers = new();
        [ThreadStatic] private static ThreadBuffer? _localBuffer;

        private static long _startTimestamp;
        private static int _pid;
        private static string? _outputPath;

        private sealed class ThreadBuffer
        {
            public int ThreadId;
            public string ThreadName = "";
            // Reasonable starting capacity — a 30 Hz game with ~100 scopes/tick
            // produces ~3000 events/sec, so this covers ~1.3 seconds before resizing.
            public List<TraceEvent> Events = new(4096);
        }

        private struct TraceEvent
        {
            public string Name;
            public string Category;
            public long StartMicros;
            public long DurMicros;
        }

        /// <summary>
        /// Initializes the profiler. Safe to call when <paramref name="enabled"/>
        /// is false — does no I/O and leaves the profiler dormant. When enabled,
        /// creates <paramref name="logDir"/> if missing and reserves an output
        /// path of the form profile_YYYY-MM-DD_HH-MM-SS.json.
        /// </summary>
        public static void Init(string logDir, bool enabled)
        {
            if (!enabled)
            {
                Enabled = false;
                return;
            }

            Directory.CreateDirectory(logDir);
            _pid = Environment.ProcessId;
            var ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _outputPath = Path.Combine(logDir, $"profile_{ts}.json");
            _startTimestamp = Stopwatch.GetTimestamp();
            Enabled = true;
        }

        /// <summary>
        /// Flushes buffered events to disk and disables further recording.
        /// </summary>
        public static void Shutdown()
        {
            if (!Enabled) return;
            Enabled = false;
            Flush();
        }

        public static ScopeHandle Scope(string name, string category = "game")
        {
            if (!Enabled) return default;
            return new ScopeHandle(name, category, NowMicros());
        }

        private static long NowMicros()
        {
            long delta = Stopwatch.GetTimestamp() - _startTimestamp;
            return delta * 1_000_000L / Stopwatch.Frequency;
        }

        private static ThreadBuffer GetLocalBuffer()
        {
            var buf = _localBuffer;
            if (buf == null)
            {
                buf = new ThreadBuffer
                {
                    ThreadId = Environment.CurrentManagedThreadId,
                    ThreadName = Thread.CurrentThread.Name ?? $"Thread-{Environment.CurrentManagedThreadId}",
                };
                _buffers[buf.ThreadId] = buf;
                _localBuffer = buf;
            }
            return buf;
        }

        // Called from Scope.Dispose. Nested struct has access to private members.
        private static void Record(string name, string category, long startMicros, long endMicros)
        {
            GetLocalBuffer().Events.Add(new TraceEvent
            {
                Name = name,
                Category = category,
                StartMicros = startMicros,
                DurMicros = endMicros - startMicros,
            });
        }

        private static void Flush()
        {
            if (_outputPath == null) return;
            try
            {
                using var sw = new StreamWriter(_outputPath);
                sw.Write("{\"traceEvents\":[");
                bool first = true;
                foreach (var kvp in _buffers)
                {
                    var buf = kvp.Value;
                    if (!first) sw.Write(",");
                    first = false;
                    // Metadata event so Perfetto labels the thread row.
                    sw.Write("{\"name\":\"thread_name\",\"ph\":\"M\",\"pid\":");
                    sw.Write(_pid);
                    sw.Write(",\"tid\":");
                    sw.Write(buf.ThreadId);
                    sw.Write(",\"args\":{\"name\":");
                    WriteJsonString(sw, buf.ThreadName);
                    sw.Write("}}");

                    foreach (var ev in buf.Events)
                    {
                        sw.Write(",{\"name\":");
                        WriteJsonString(sw, ev.Name);
                        sw.Write(",\"cat\":");
                        WriteJsonString(sw, ev.Category);
                        sw.Write(",\"ph\":\"X\",\"ts\":");
                        sw.Write(ev.StartMicros);
                        sw.Write(",\"dur\":");
                        sw.Write(ev.DurMicros);
                        sw.Write(",\"pid\":");
                        sw.Write(_pid);
                        sw.Write(",\"tid\":");
                        sw.Write(buf.ThreadId);
                        sw.Write("}");
                    }
                }
                sw.Write("]}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Profiler: failed to write trace to {_outputPath}: {ex.Message}");
            }
        }

        private static void WriteJsonString(StreamWriter sw, string s)
        {
            sw.Write('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sw.Write("\\\""); break;
                    case '\\': sw.Write("\\\\"); break;
                    case '\n': sw.Write("\\n"); break;
                    case '\r': sw.Write("\\r"); break;
                    case '\t': sw.Write("\\t"); break;
                    default:
                        if (c < 0x20) sw.Write($"\\u{(int)c:x4}");
                        else sw.Write(c);
                        break;
                }
            }
            sw.Write('"');
        }

        public readonly struct ScopeHandle : IDisposable
        {
            private readonly string _name;
            private readonly string _category;
            private readonly long _startMicros;
            private readonly bool _active;

            internal ScopeHandle(string name, string category, long startMicros)
            {
                _name = name;
                _category = category;
                _startMicros = startMicros;
                _active = true;
            }

            public void Dispose()
            {
                if (!_active || !Profiler.Enabled) return;
                Profiler.Record(_name, _category, _startMicros, Profiler.NowMicros());
            }
        }
    }
}
