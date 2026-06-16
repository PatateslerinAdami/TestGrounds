using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;

namespace Spells;

public class SorakaBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() { TriggersSpellCasts = true };
    public void OnActivate(ObjAIBase owner, Spell spell) { }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}

public class SorakaBasicAttack2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() { TriggersSpellCasts = true };
    public void OnActivate(ObjAIBase owner, Spell spell) { }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}

public class SorakaCritAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() { TriggersSpellCasts = true };
    public void OnActivate(ObjAIBase owner, Spell spell) { }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}
