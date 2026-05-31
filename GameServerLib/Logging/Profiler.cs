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
            public long StartNanos;
            public long DurNanos;
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
            return new ScopeHandle(name, category, NowNanos());
        }

        // Nanosecond clock relative to Init(). Computed via double to avoid the
        // long-multiply overflow that would otherwise hit on Stopwatch sources
        // running at 1 GHz (Linux) after only ~9 seconds of trace. Double has
        // ~15 digits of precision, so it stays accurate well past any plausible
        // profiling session.
        //
        // Internal precision matters: at microseconds, scopes that start within
        // the same microsecond (e.g. Tick + DrainInboundEvents at the top of a
        // tick) round to identical timestamps, which previously confused
        // Perfetto's BEGIN/END stack pairing into inverted nesting.
        private static long NowNanos()
        {
            long delta = Stopwatch.GetTimestamp() - _startTimestamp;
            return (long)((double)delta * 1_000_000_000d / Stopwatch.Frequency);
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
        private static void Record(string name, string category, long startNanos, long endNanos)
        {
            GetLocalBuffer().Events.Add(new TraceEvent
            {
                Name = name,
                Category = category,
                StartNanos = startNanos,
                DurNanos = endNanos - startNanos,
            });
        }

        // Build the Perfetto Trace message in memory, then serialize it to the
        // output file in one shot. Each thread becomes a TrackDescriptor; each
        // scope becomes a SLICE_BEGIN + SLICE_END pair on its thread's track.
        // Perfetto requires a unique trusted_packet_sequence_id per producer;
        // we use the thread id so the sequences never collide.
        //
        // Per-event packets must be emitted in true timeline order, not in
        // recording order. Events are appended to the buffer at Dispose() time
        // (i.e. in END timestamp order), which puts inner scopes before their
        // parents in the buffer. Emitting in buffer order with co-incident
        // BEGIN timestamps confuses Perfetto's stack pairing and produces
        // inverted nesting. We fix this by materializing a merged timeline of
        // (timestamp, kind) entries and sorting once at flush time with proper
        // tie-breakers (see TimelineEntry.Compare below).
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

                    // Materialize the per-thread timeline: one BEGIN entry and
                    // one END entry per scope, then sort into emission order.
                    var timeline = new List<TimelineEntry>(buf.Events.Count * 2);
                    for (int i = 0; i < buf.Events.Count; i++)
                    {
                        var ev = buf.Events[i];
                        timeline.Add(new TimelineEntry(ev.StartNanos, isBegin: true, ev.DurNanos, i));
                        timeline.Add(new TimelineEntry(ev.StartNanos + ev.DurNanos, isBegin: false, ev.DurNanos, i));
                    }
                    timeline.Sort(TimelineEntry.Compare);

                    foreach (var entry in timeline)
                    {
                        var ev = buf.Events[entry.EventIndex];
                        if (entry.IsBegin)
                        {
                            var beginEvent = new TrackEvent
                            {
                                Type = TrackEvent.Types.Type.SliceBegin,
                                Name = ev.Name,
                                TrackUuid = trackUuid,
                            };
                            beginEvent.Categories.Add(ev.Category);

                            trace.Packet.Add(new TracePacket
                            {
                                Timestamp = (ulong)entry.TimestampNanos,
                                TrustedPacketSequenceId = sequenceId,
                                TrackEvent = beginEvent,
                            });
                        }
                        else
                        {
                            trace.Packet.Add(new TracePacket
                            {
                                Timestamp = (ulong)entry.TimestampNanos,
                                TrustedPacketSequenceId = sequenceId,
                                TrackEvent = new TrackEvent
                                {
                                    Type = TrackEvent.Types.Type.SliceEnd,
                                    TrackUuid = trackUuid,
                                },
                            });
                        }
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

        // Timeline entry used only inside Flush to produce a correctly-ordered
        // BEGIN/END packet stream. Sorted with these tie-break rules so that
        // even at identical nanosecond timestamps (extremely rare but possible)
        // Perfetto's BEGIN/END stack pairs nest the right way:
        //   1. timestamp ASC
        //   2. at the same timestamp, ENDs come before BEGINs
        //      (so a closing scope is popped before the next one opens)
        //   3. at the same timestamp, multiple BEGINs sort by duration DESC
        //      (outer = longer-lived scope opens first)
        //   4. at the same timestamp, multiple ENDs sort by duration ASC
        //      (inner = shorter-lived scope closes first)
        private readonly struct TimelineEntry
        {
            public readonly long TimestampNanos;
            public readonly bool IsBegin;
            public readonly long DurNanos;
            public readonly int EventIndex;

            public TimelineEntry(long ts, bool isBegin, long dur, int idx)
            {
                TimestampNanos = ts;
                IsBegin = isBegin;
                DurNanos = dur;
                EventIndex = idx;
            }

            public static int Compare(TimelineEntry a, TimelineEntry b)
            {
                int c = a.TimestampNanos.CompareTo(b.TimestampNanos);
                if (c != 0) return c;
                // ENDs before BEGINs at the same instant.
                if (a.IsBegin != b.IsBegin) return a.IsBegin ? 1 : -1;
                // Two BEGINs: outer (longer duration) first.
                if (a.IsBegin) return b.DurNanos.CompareTo(a.DurNanos);
                // Two ENDs: inner (shorter duration) first.
                return a.DurNanos.CompareTo(b.DurNanos);
            }
        }

        public readonly struct ScopeHandle : IDisposable
        {
            private readonly string _name;
            private readonly string _category;
            private readonly long _startNanos;
            private readonly bool _active;

            internal ScopeHandle(string name, string category, long startNanos)
            {
                _name = name;
                _category = category;
                _startNanos = startNanos;
                _active = true;
            }

            public void Dispose()
            {
                if (!_active || !Profiler.Enabled) return;
                Profiler.Record(_name, _category, _startNanos, Profiler.NowNanos());
            }
        }
    }
}
