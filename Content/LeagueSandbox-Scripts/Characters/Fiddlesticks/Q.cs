using System.Numerics;
using Buffs;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells
{
    public class Terrify : ISpellScript
    {
        private ObjAIBase _owner;
        private AttackableUnit _target;
        public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
        {
            IsDamagingSpell = true,
            TriggersSpellCasts = true,

        };
        
        public void OnActivate(ObjAIBase owner, Spell spell) {
            _owner = owner;
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
            _target = target;
        }
        
        public void OnSpellPostCast(Spell spell)
        {
            var duration = 1.25f + 0.25f * (spell.CastInfo.SpellLevel - 1);
            /*var fear = new Fear()
            {
                RandomDirection = true,
                slowPercent = 0.5f
            };*/
            AddBuff("Flee", duration, 1, spell, _target, _owner);
            //AddBuff(fear, "Fear", 1.25f, 1, spell, target, spell.CastInfo.Owner);

        }
    }
}
