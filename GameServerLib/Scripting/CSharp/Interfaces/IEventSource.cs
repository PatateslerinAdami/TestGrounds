namespace GameServerCore.Scripting.CSharp
{
    public interface IEventSource
    {
        uint ScriptNameHash { get; }
        IEventSource ParentScript { get; }

        // Death-recap frame selection (Riot: scriptBaseBuff::IsDeathRecapSource, BUFF-only —
        // Spell::Lua::scriptBase hard-returns false). When a damage/heal event resolves its source
        // from the ambient script stack (GetDeathRecapEventSource), a frame with this flag set is
        // credited over the enclosing root frame. Default false; only buff scripts opt in.
        bool IsDeathRecapSource => false;
    }
}