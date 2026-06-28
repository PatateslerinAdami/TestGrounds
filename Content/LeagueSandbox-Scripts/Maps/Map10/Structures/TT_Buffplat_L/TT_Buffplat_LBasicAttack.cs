using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;

public class TT_Buffplat_LBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell    = true
    };
}