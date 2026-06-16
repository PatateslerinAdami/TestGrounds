using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;

/// <summary>
/// Stub: Q heal return handled directly in SorakaQ instead.
/// Exists only to satisfy the game data's ExtraSlot reference.
/// </summary>
public class SorakaQReturnMissile : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = false,
        IsDamagingSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}
