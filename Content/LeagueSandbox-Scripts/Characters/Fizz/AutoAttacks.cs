using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells
{
    public class FizzBasicAttack : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true
        };
    }

    public class FizzBasicAttack2 : FizzBasicAttack
    {
    }

    public class FizzCritAttack : FizzBasicAttack
    {
    }
}
