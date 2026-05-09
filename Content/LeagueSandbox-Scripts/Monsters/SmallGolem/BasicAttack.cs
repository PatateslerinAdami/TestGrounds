using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;

public class SmallGolemBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };
}