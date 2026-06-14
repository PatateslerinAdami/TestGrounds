using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;


namespace Spells;

public class Blue_Minion_MechMeleeBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true
    };
}

public class Blue_Minion_MechMeleeBasicAttack2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true
    };
}