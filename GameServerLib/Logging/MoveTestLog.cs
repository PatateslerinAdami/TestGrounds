using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace LeagueSandbox.GameServer.Logging
{
    /// <summary>
    /// TEST INSTRUMENTATION (movement/collision live test). A lock-free, off-thread logger:
    /// the game-loop thread only enqueues pre-formatted strings (cheap), while a single background
    /// thread drains the queue and writes to <c>movetest.log</c> with a buffered writer. This keeps
    /// all I/O off the tick — synchronous log4net console writes were stalling the game loop and
    /// causing the in-game lag during testing. Remove together with the `[WIRE]/[C2C]/[GHOST+]/
    /// [SEP]/[STUCK]` log calls once the test is done.
    /// </summary>
    public static class MoveTestLog
    {
        // Master switch — flip to false to disable all test logging with zero per-call cost.
        public static volatile bool Enabled = true;

        private static readonly BlockingCollection<string> _queue =
            new BlockingCollection<string>(new ConcurrentQueue<string>(), 1 << 16);
        private static readonly long _startTick = Environment.TickCount64;
        private static readonly Thread _worker;

        static MoveTestLog()
        {
            _worker = new Thread(Drain)
            {
                IsBackground = true,
                Name = "MoveTestLog",
                Priority = ThreadPriority.BelowNormal
            };
            _worker.Start();
        }

        /// <summary>
        /// Enqueue a line for off-thread writing. Prepends a millisecond timestamp (relative to
        /// process start) so packet cadence/gaps are recoverable from the file. Non-blocking: if the
        /// queue is full the line is dropped rather than stalling the game loop.
        /// </summary>
        public static void Log(string line)
        {
            if (!Enabled)
            {
                return;
            }
            // TryAdd with timeout 0 => never blocks the caller (drops under extreme backpressure).
            _queue.TryAdd($"{Environment.TickCount64 - _startTick} {line}", 0);
        }

        private static void Drain()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "movetest.log");
            try
            {
                using var w = new StreamWriter(path, append: false) { AutoFlush = false };
                Console.WriteLine($"[MoveTestLog] writing movement/collision test logs to: {path}");

                int sinceFlush = 0;
                foreach (var line in _queue.GetConsumingEnumerable())
                {
                    w.WriteLine(line);
                    // Flush in batches, or whenever the queue drains, so the tail isn't lost for long.
                    if (++sinceFlush >= 128 || _queue.Count == 0)
                    {
                        w.Flush();
                        sinceFlush = 0;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[MoveTestLog] writer stopped: {e.Message}");
            }
        }
    }
}
