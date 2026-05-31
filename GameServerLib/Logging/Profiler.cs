using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Google.Protobuf;
using Perfetto.Protos;

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
            // With BEGIN+END split, each scope is two entries.
            public List<TraceEvent> Events = new(8192);
        }

        // Each TraceEvent represents one BEGIN or END marker on the timeline.
        // Name == null indicates an END event (Category is also unused for ENDs).
        // Recording two entries per scope at execution time (BEGIN at Scope(),
        // END at Dispose()) gives the buffer a natural execution-order layout,
        // so Flush() can stream packets out in buffer order without sorting and
        // Perfetto's BEGIN/END stack pairs nest correctly.
        private struct TraceEvent
        {
            public string? Name;
            public string? Category;
            public long TimestampNanos;
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
            // Record BEGIN immediately at scope creation. ScopeHandle then only
            // needs to remember whether to also record an END at Dispose time.
            GetLocalBuffer().Events.Add(new TraceEvent
            {
                Name = name,
                Category = category,
                TimestampNanos = NowNanos(),
            });
            return new ScopeHandle(active: true);
        }

        // Nanosecond clock relative to Init(). Computed via double to avoid the
        // long-multiply overflow that would otherwise hit on Stopwatch sources
        // running at 1 GHz (Linux) after only ~9 seconds of trace. Double has
        // ~15 digits of precision, so it stays accurate well past any plausible
        // profiling session.
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
        // Records an END marker. Name is left null to mark it as an END.
        private static void RecordEnd()
        {
            GetLocalBuffer().Events.Add(new TraceEvent
            {
                Name = null,
                Category = null,
                TimestampNanos = NowNanos(),
            });
        }

        // Streams Perfetto TracePackets directly to the output file rather than
        // building a single in-memory Trace message. Two reasons this matters:
        //
        //   1. Google.Protobuf serializes message sizes as int32, so a single
        //      Trace larger than ~2 GB serialized would throw at write time.
        //      A long bot match with EzrealBot instrumentation easily exceeds
        //      that. Streaming each packet keeps every serialized message tiny.
        //
        //   2. Memory pressure: building the full Trace and an auxiliary sort
        //      timeline both held the entire event set in memory more than once.
        //      Per-packet streaming keeps peak memory at roughly the buffer
        //      itself.
        //
        // Wire format note: Trace is `repeated TracePacket packet = 1`. Protobuf's
        // canonical encoding for a repeated field is `[tag][length][body]` per
        // element, and concatenated encodings of a message are equivalent to a
        // single encoding with the union of fields. Writing each packet as
        // `WriteTag(1, LengthDelimited) + WriteMessage(packet)` therefore
        // produces a stream byte-identical to what Trace.WriteTo would emit.
        private static void Flush()
        {
            if (_outputPath == null) return;

            int totalEvents = 0;
            foreach (var kvp in _buffers) totalEvents += kvp.Value.Events.Count;
            // Loud start-of-flush log so it's obvious in console output whether
            // we even got here. The dev's "no file appeared" case usually means
            // an exception thrown before File.Create.
            Console.WriteLine($"Profiler: flushing {totalEvents} events from {_buffers.Count} thread(s) to {_outputPath}");

            try
            {
                using var fs = File.Create(_outputPath);
                using var cos = new CodedOutputStream(fs);

                foreach (var kvp in _buffers)
                {
                    var buf = kvp.Value;
                    uint sequenceId = (uint)buf.ThreadId;
                    ulong trackUuid = (ulong)(uint)buf.ThreadId;

                    // Track descriptor first so Perfetto labels the row.
                    WritePacket(cos, new TracePacket
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

                    // Events are in execution order (BEGIN appended at Scope()
                    // time, END appended at Dispose() time), so streaming them
                    // in buffer order is naturally correct for stack pairing.
                    foreach (var ev in buf.Events)
                    {
                        if (ev.Name != null)
                        {
                            var begin = new TrackEvent
                            {
                                Type = TrackEvent.Types.Type.SliceBegin,
                                Name = ev.Name,
                                TrackUuid = trackUuid,
                            };
                            if (ev.Category != null) begin.Categories.Add(ev.Category);

                            WritePacket(cos, new TracePacket
                            {
                                Timestamp = (ulong)ev.TimestampNanos,
                                TrustedPacketSequenceId = sequenceId,
                                TrackEvent = begin,
                            });
                        }
                        else
                        {
                            WritePacket(cos, new TracePacket
                            {
                                Timestamp = (ulong)ev.TimestampNanos,
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

                cos.Flush();
                Console.WriteLine($"Profiler: trace written successfully to {_outputPath}");
            }
            catch (Exception ex)
            {
                // Be loud about this. The most common reason for a missing trace
                // file is an exception thrown here, and a single Console.WriteLine
                // is easy to miss in a busy server log.
                Console.Error.WriteLine($"Profiler: FAILED to write trace to {_outputPath}");
                Console.Error.WriteLine($"Profiler: exception was {ex}");
            }
        }

        private static void WritePacket(CodedOutputStream cos, TracePacket packet)
        {
            // Tag for Trace.packet (field number 1, length-delimited wire type).
            cos.WriteTag(1, WireFormat.WireType.LengthDelimited);
            cos.WriteMessage(packet);
        }

        public readonly struct ScopeHandle : IDisposable
        {
            private readonly bool _active;

            internal ScopeHandle(bool active) { _active = active; }

            public void Dispose()
            {
                // _active being true means we recorded a BEGIN at Scope() time,
                // so we owe an END here to keep the stack balanced. We do *not*
                // re-check Profiler.Enabled: if the profiler was on at Scope()
                // and got turned off mid-scope, dropping the END would leave a
                // dangling BEGIN in the buffer and confuse Perfetto.
                if (!_active) return;
                Profiler.RecordEnd();
            }
        }
    }
}
