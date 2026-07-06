using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;


namespace Spells;

public class SRU_ChaosMinionMeleeBasicAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true
    };
}