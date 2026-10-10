using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells
{
    public class FioraBasicAttack : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true
        };
    }

    public class FioraBasicAttack2 : FioraBasicAttack
    {
    }

    public class FioraCritAttack : FioraBasicAttack
    {
    }

    public class FioraBasicAttackFast : FioraBasicAttack
    {
    }
}
