using Buffs;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class Terrify : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = true,

        };
        public void OnSpellPostCast(Spell spell)
        {
            var target = spell.CastInfo.Targets[0].Unit;
            var fear = new Fear()
            {
                RandomDirection = true,
                slowPercent = 0.5f
            };
            AddBuff(fear, "Fear", 1.25f, 1, spell, target, spell.CastInfo.Owner);

        }
    }
}
