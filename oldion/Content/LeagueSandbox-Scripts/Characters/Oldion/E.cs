using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class Enrage : ISpellScript
{
    private ObjAIBase _owner;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _owner = owner; }

    public void OnSpellPostCast(Spell spell)
    {
        if (_owner.HasBuff("EnrageBuff"))
            RemoveBuff(_owner, "EnrageBuff");
        else
            AddBuff("EnrageBuff", 25000f, 1, spell, _owner, _owner, true);
    }
}
