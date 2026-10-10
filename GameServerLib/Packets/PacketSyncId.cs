using System;
using System.Threading;

namespace PacketDefinitions420
{
    internal static class PacketSyncId
    {
        private static int _last = Environment.TickCount & int.MaxValue;

        internal static int Next()
        {
            while (true)
            {
                int previous = Volatile.Read(ref _last);
                int clock = Environment.TickCount & int.MaxValue;
                int delta = clock - previous;
                int next = delta > 0 && delta < 0x40000000
                    ? clock : (previous + 1) & int.MaxValue;
                if (Interlocked.CompareExchange(ref _last, next, previous) == previous)
                    return next;
            }
        }
    }
}
