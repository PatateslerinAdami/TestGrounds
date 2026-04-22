using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;


namespace Spells;

public class EvelynnBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true
    };
}

public class EvelynnBasicAttack2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true
    };
}

public class EvelynnBasicAttack3 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true
    };
}

public class EvelynnCritAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true
    };
}