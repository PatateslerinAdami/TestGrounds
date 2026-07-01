using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;


namespace Spells;

public class MasterYiBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true
    };
}

public class MasterYiBasicAttack2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true
    };
}

public class MasterYiCritAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true
    };
}

public class MasterYiDoubleStrike : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}