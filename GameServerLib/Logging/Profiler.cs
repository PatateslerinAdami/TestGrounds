using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Google.Protobuf;
using Perfetto.Protos;
// Alias to disambiguate from System.Diagnostics.Trace which is in scope
// via System.Diagnostics.Stopwatch.
using PerfettoTrace = Perfetto.Protos.Trace;

namespace LeagueSandbox.GameServer.Logging
{
    /// <summary>
    /// CPU profiler that records named scopes into a Perfetto-native protobuf
    /// trace file. Drag the resulting profile_*.perfetto-trace into
    /// https://ui.perfetto.dev to view a flame chart of game-loop work.
    ///
    /// Disabled by default. When disabled, Scope() returns a default struct
    /// whose Dispose() is a no-op; cost reduces to one boolean check per
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
            // Reasonable starting capacity. A 30 Hz game with ~100 scopes/tick
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
        /// is false; does no I/O and leaves the profiler dormant. When enabled,
        /// creates <paramref name="logDir"/> if missing and reserves an output
        /// path of the form profile_YYYY-MM-DD_HH-MM-SS.perfetto-trace.
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
            _outputPath = Path.Combine(logDir, $"profile_{ts}.perfetto-trace");
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

        // Called from ScopeHandle.Dispose. Nested struct has access to private members.
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

        // Build the Perfetto Trace message in memory, then serialize it to the
        // output file in one shot. Each thread becomes a TrackDescriptor; each
        // scope becomes a SLICE_BEGIN + SLICE_END pair on its thread's track.
        // Perfetto requires a unique trusted_packet_sequence_id per producer;
        // we use the thread id so the sequences never collide.
        private static void Flush()
        {
            if (_outputPath == null) return;
            try
            {
                var trace = new PerfettoTrace();

                foreach (var kvp in _buffers)
                {
                    var buf = kvp.Value;
                    uint sequenceId = (uint)buf.ThreadId;
                    ulong trackUuid = (ulong)(uint)buf.ThreadId;

                    // Declare the thread track up front so Perfetto labels the row.
                    trace.Packet.Add(new TracePacket
                    {
                        TrustedPacketSequenceId = sequenceId,
                        TrackDescriptor = new TrackDescriptor
                        {
                            Uuid = trackUuid,
                            Name = buf.ThreadName,
                            Thread = new ThreadDescriptor
                            {
                                Pid = _pid,
                                Tid = buf.ThreadId,
                                ThreadName = buf.ThreadName,
                            },
                        },
                    });

                    foreach (var ev in buf.Events)
                    {
                        // TracePacket.timestamp defaults to nanoseconds; we store
                        // microseconds, so multiply on the way out.
                        ulong startNs = (ulong)(ev.StartMicros * 1000);
                        ulong endNs   = (ulong)((ev.StartMicros + ev.DurMicros) * 1000);

                        var beginEvent = new TrackEvent
                        {
                            Type = TrackEvent.Types.Type.SliceBegin,
                            Name = ev.Name,
                            TrackUuid = trackUuid,
                        };
                        beginEvent.Categories.Add(ev.Category);

                        trace.Packet.Add(new TracePacket
                        {
                            Timestamp = startNs,
                            TrustedPacketSequenceId = sequenceId,
                            TrackEvent = beginEvent,
                        });
                        trace.Packet.Add(new TracePacket
                        {
                            Timestamp = endNs,
                            TrustedPacketSequenceId = sequenceId,
                            TrackEvent = new TrackEvent
                            {
                                Type = TrackEvent.Types.Type.SliceEnd,
                                TrackUuid = trackUuid,
                            },
                        });
                    }
                }

                using var fs = File.Create(_outputPath);
                trace.WriteTo(fs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Profiler: failed to write trace to {_outputPath}: {ex.Message}");
            }
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
