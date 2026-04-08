using System;

namespace LeagueSandbox.GameServer.Scripting.CSharp;

/// <summary>
///     Utility for converting per-frame <c>diff</c> (milliseconds) into fixed-period ticks.
/// </summary>
public struct PeriodicTicker {
    private float _elapsedMs;
    private bool  _firedImmediate;
    private int   _totalTicks;

    /// <summary>
    ///     Consumes elapsed frame time and returns how many ticks should execute this update.
    /// </summary>
    /// <param name="diffMs">Elapsed time since last update in milliseconds.</param>
    /// <param name="periodMs">Tick interval in milliseconds.</param>
    /// <param name="fireImmediately">
    ///     If true, emits one tick the first time this is called after <see cref="Reset"/>.
    /// </param>
    /// <param name="maxTicksPerUpdate">
    ///     Safety cap to avoid runaway work after long stalls.
    /// </param>
    /// <param name="maxTotalTicks">
    ///     Optional lifetime cap for total ticks emitted since last <see cref="Reset"/>.
    /// </param>
    public int ConsumeTicks(
        float diffMs,
        float periodMs,
        bool  fireImmediately  = false,
        int   maxTicksPerUpdate = 4,
        int   maxTotalTicks    = int.MaxValue
    ) {
        if (periodMs <= 0.0f) return 0;
        if (maxTicksPerUpdate < 1) maxTicksPerUpdate = 1;
        if (maxTotalTicks < 0) maxTotalTicks = 0;
        if (_totalTicks >= maxTotalTicks) return 0;

        var ticks = 0;
        if (fireImmediately && !_firedImmediate && _totalTicks < maxTotalTicks) {
            _firedImmediate = true;
            ticks++;
        }

        _elapsedMs += MathF.Max(0.0f, diffMs);
        while (_elapsedMs >= periodMs
               && ticks < maxTicksPerUpdate
               && _totalTicks + ticks < maxTotalTicks) {
            _elapsedMs -= periodMs;
            ticks++;
        }

        // Keep backlog bounded if we hit the cap this frame.
        if (ticks >= maxTicksPerUpdate && _elapsedMs > periodMs) {
            _elapsedMs = periodMs;
        }

        _totalTicks += ticks;
        return ticks;
    }

    /// <summary>
    ///     Returns remaining milliseconds until the next tick would fire.
    /// </summary>
    /// <param name="periodMs">Tick interval in milliseconds.</param>
    /// <param name="fireImmediately">
    ///     If true and the immediate tick has not fired yet, returns 0.
    /// </param>
    /// <param name="maxTotalTicks">
    ///     Optional lifetime cap for total ticks emitted since last <see cref="Reset"/>.
    /// </param>
    /// <returns>
    ///     Milliseconds until next tick. Returns 0 if tick is due now, period is invalid, or total tick cap is reached.
    /// </returns>
    public float GetRemainingMsUntilNextTick(
        float periodMs,
        bool  fireImmediately = false,
        int   maxTotalTicks   = int.MaxValue
    ) {
        if (periodMs <= 0.0f) return 0.0f;
        if (maxTotalTicks < 0) maxTotalTicks = 0;
        if (_totalTicks >= maxTotalTicks) return 0.0f;
        if (fireImmediately && !_firedImmediate) return 0.0f;

        var remaining = periodMs - _elapsedMs;
        return remaining > 0.0f ? remaining : 0.0f;
    }

    public void Reset() {
        _elapsedMs      = 0.0f;
        _firedImmediate = false;
        _totalTicks     = 0;
    }
}
