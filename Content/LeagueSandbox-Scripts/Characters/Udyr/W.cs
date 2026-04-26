using System.Numerics;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class UdyrTurtleStance : ISpellScript {
    private ObjAIBase _owner;
    private Spell     _spell;

    public StatsModifier StatsModifier { get; } = new();

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _spell = spell;
        _owner = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        AddBuff("UdyrTurtleStance",     25000f, 1, spell, owner,  owner, true);
        AddBuff("UdyrTurtleActivation", 5f,     1, spell, target, owner);
    }
}

public class UdyrTurtleAttack : ISpellScript {
    private ObjAIBase _udyr;
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _udyr = owner;
    }
}