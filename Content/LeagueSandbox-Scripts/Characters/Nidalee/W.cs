using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

// Human W — Bushwhack (trap)
public class Bushwhack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}

// Cougar W — Pounce (dash)
public class Pounce : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}
