using System;
using System.Collections.Generic;

namespace GameServerCore.Scripting.CSharp
{
    /// <summary>
    /// Ambient "currently-executing script" context — the server-side analog of Riot's
    /// <c>Spell::Lua::sScriptHistory</c> stack. A script frame is pushed around each script-callback
    /// invocation at the dispatch choke points (e.g. ApiEventManager.Publish), and damage/heal
    /// attribution (<c>TakeDamage</c>/<c>TakeHeal</c>) falls back to <see cref="Current"/> when no
    /// explicit <c>sourceScript</c> is supplied. This mirrors Riot, where the Lua <c>dealDamage</c>
    /// API never takes an event source — it is resolved from whichever script frame is running.
    ///
    /// The game simulation is single-threaded, so a per-thread stack is sufficient; <c>[ThreadStatic]</c>
    /// keeps it isolated should any script ever be driven off the main loop. Push/pop is balanced via
    /// try/finally at the call sites (Riot uses RAII around <c>EventFrame</c>).
    /// </summary>
    public static class ScriptContext
    {
        [ThreadStatic] private static Stack<IEventSource> _stack;
        private static Stack<IEventSource> Stack => _stack ??= new Stack<IEventSource>();

        /// <summary>The script frame currently executing, or null when no script frame is active.</summary>
        public static IEventSource Current => Stack.Count > 0 ? Stack.Peek() : null;

        /// <summary>
        /// Resolves the death-recap source for a damage/heal dealt in the current script context —
        /// the port of Riot's <c>Spell::Lua::GetDeathRecapEventSource</c>. Walks the frame stack from
        /// newest to oldest and returns the first frame that opted in via <see cref="IEventSource.IsDeathRecapSource"/>
        /// (a flagged buff overrides the enclosing spell); if none is flagged it falls back to the ROOT
        /// frame — the outermost/first-pushed script that started the chain, not the innermost. Returns
        /// null when no script frame is active (engine-internal damage).
        /// </summary>
        public static IEventSource ResolveDeathRecapSource()
        {
            IEventSource root = null;
            // Stack<T> enumerates top→bottom (LIFO), so the first flagged frame is the newest one, and
            // the last frame visited is the root.
            foreach (var frame in Stack)
            {
                if (frame == null)
                {
                    continue;
                }
                if (frame.IsDeathRecapSource)
                {
                    return frame;
                }
                root = frame;
            }
            return root;
        }

        /// <summary>Pushes a script frame. Must be paired with <see cref="Pop"/> in a finally block.</summary>
        public static void Push(IEventSource source) => Stack.Push(source);

        /// <summary>Pops the top script frame. No-op if the stack is empty (defensive).</summary>
        public static void Pop()
        {
            if (Stack.Count > 0)
            {
                Stack.Pop();
            }
        }

        /// <summary>
        /// Pushes a script frame for the lifetime of a <c>using</c> block, e.g.
        /// <c>using (ScriptContext.Enter(this)) Script.OnActivate(...);</c>. The returned struct pops
        /// the frame on dispose (allocation-free), giving RAII-style balancing at direct call sites
        /// that don't go through ApiEventManager.
        /// </summary>
        public static Frame Enter(IEventSource source)
        {
            Push(source);
            return default;
        }

        public readonly struct Frame : IDisposable
        {
            public void Dispose() => Pop();
        }
    }
}
