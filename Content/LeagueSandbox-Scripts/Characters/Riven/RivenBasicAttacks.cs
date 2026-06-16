using GameServerCore.Scripting.CSharp;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;

namespace Spells
{
    public class RivenBasicAttack : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };

        public void OnActivate(ObjAIBase owner, Spell spell) { }
        public void OnDeactivate(ObjAIBase owner, Spell spell) { }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
        public void OnSpellCast(Spell spell) { }
        public void OnSpellPostCast(Spell spell) { }
        public void OnUpdate(float diff) { }
    }

    public class RivenBasicAttack2 : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };

        public void OnActivate(ObjAIBase owner, Spell spell) { }
        public void OnDeactivate(ObjAIBase owner, Spell spell) { }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
        public void OnSpellCast(Spell spell) { }
        public void OnSpellPostCast(Spell spell) { }
        public void OnUpdate(float diff) { }
    }

    public class RivenCritAttack : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata => new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
        };

        public void OnActivate(ObjAIBase owner, Spell spell) { }
        public void OnDeactivate(ObjAIBase owner, Spell spell) { }
        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
        public void OnSpellCast(Spell spell) { }
        public void OnSpellPostCast(Spell spell) { }
        public void OnUpdate(float diff) { }
    }
}
