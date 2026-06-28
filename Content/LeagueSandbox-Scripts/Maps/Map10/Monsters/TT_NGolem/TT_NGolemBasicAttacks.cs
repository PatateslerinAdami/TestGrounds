using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;

public class TT_NGolemBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell    = true
    };
}