using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;

public class AncientGolemInitialAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };
}

public class AncientGolemBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };
}

public class AncientGolemBasicAttack2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };
}

public class AncientGolemBasicAttack3 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };
}