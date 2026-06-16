using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;

namespace Spells;

// Melee (Hammer) basic attacks
public class JayceBasicAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new() { TriggersSpellCasts = true };
    public void OnActivate(ObjAIBase o, Spell s) { } public void OnDeactivate(ObjAIBase o, Spell s) { }
    public void OnSpellPreCast(ObjAIBase o, Spell s, AttackableUnit t, Vector2 st, Vector2 e) { }
    public void OnSpellCast(Spell s) { } public void OnSpellPostCast(Spell s) { } public void OnUpdate(float d) { }
}
public class JayceBasicAttack2 : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new() { TriggersSpellCasts = true };
    public void OnActivate(ObjAIBase o, Spell s) { } public void OnDeactivate(ObjAIBase o, Spell s) { }
    public void OnSpellPreCast(ObjAIBase o, Spell s, AttackableUnit t, Vector2 st, Vector2 e) { }
    public void OnSpellCast(Spell s) { } public void OnSpellPostCast(Spell s) { } public void OnUpdate(float d) { }
}
public class JayceCritAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new() { TriggersSpellCasts = true };
    public void OnActivate(ObjAIBase o, Spell s) { } public void OnDeactivate(ObjAIBase o, Spell s) { }
    public void OnSpellPreCast(ObjAIBase o, Spell s, AttackableUnit t, Vector2 st, Vector2 e) { }
    public void OnSpellCast(Spell s) { } public void OnSpellPostCast(Spell s) { } public void OnUpdate(float d) { }
}

// Ranged (Cannon) basic attacks
public class JayceRangedAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new() { TriggersSpellCasts = true };
    public void OnActivate(ObjAIBase o, Spell s) { } public void OnDeactivate(ObjAIBase o, Spell s) { }
    public void OnSpellPreCast(ObjAIBase o, Spell s, AttackableUnit t, Vector2 st, Vector2 e) { }
    public void OnSpellCast(Spell s) { } public void OnSpellPostCast(Spell s) { } public void OnUpdate(float d) { }
}
public class JayceRangedAttack2 : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new() { TriggersSpellCasts = true };
    public void OnActivate(ObjAIBase o, Spell s) { } public void OnDeactivate(ObjAIBase o, Spell s) { }
    public void OnSpellPreCast(ObjAIBase o, Spell s, AttackableUnit t, Vector2 st, Vector2 e) { }
    public void OnSpellCast(Spell s) { } public void OnSpellPostCast(Spell s) { } public void OnUpdate(float d) { }
}
public class JayceRangedCritAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new() { TriggersSpellCasts = true };
    public void OnActivate(ObjAIBase o, Spell s) { } public void OnDeactivate(ObjAIBase o, Spell s) { }
    public void OnSpellPreCast(ObjAIBase o, Spell s, AttackableUnit t, Vector2 st, Vector2 e) { }
    public void OnSpellCast(Spell s) { } public void OnSpellPostCast(Spell s) { } public void OnUpdate(float d) { }
}
