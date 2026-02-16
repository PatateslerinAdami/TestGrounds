using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells
{
    public class KhazixR : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            NotSingleTargetSpell = true,
            IsDamagingSpell = true,
            TriggersSpellCasts = true
        };
        ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        }
        private void OnLevelUpSpell(Spell spell)
        {
            _owner.Stats.EvolvePoints++;
        }
    }
}