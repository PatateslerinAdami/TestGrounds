using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class FrostShot : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            IsDamagingSpell = true
        };
        int evolveCount = 3;
        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        }
        private void OnLevelUpSpell(Spell spell)
        {
            if(evolveCount != 0)
            { 
                _owner.Stats.EvolvePoints++; 
                evolveCount--;
            }
        }
    }
}