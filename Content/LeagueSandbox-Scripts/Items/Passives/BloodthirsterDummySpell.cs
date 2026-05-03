using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace ItemSpells
{
    public class BloodthirsterDummySpell : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            IsDamagingSpell = false,
            TriggersSpellCasts = false,
            CastingBreaksStealth = false
        };
    }
}