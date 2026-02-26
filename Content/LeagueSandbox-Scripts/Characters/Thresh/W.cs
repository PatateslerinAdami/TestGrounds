using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Spells
{
    public class ThreshW : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true,
        };

        public void OnSpellPostCast(Spell spell)
        {
            var owner = spell.CastInfo.Owner;
            var targetPos = spell.CastInfo.TargetPositionEnd;
            var startPoint = AddMinion(owner, "ThreshLantern", "ThreshLantern", owner.Position, owner.Team, skinId: owner.SkinID, ignoreCollision: true, targetable: true, isVisible: true, useSpells: true);
        }
    }

}