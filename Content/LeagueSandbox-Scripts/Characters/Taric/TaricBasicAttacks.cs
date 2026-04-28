using System.Numerics;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;

public class TaricBasicAttack : ISpellScript {
    private ObjAIBase      _owner;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true,
        TriggersSpellCasts = true
    };

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _owner  = owner;
        _target = target;
    }
}

public class TaricBasicAttack2 : ISpellScript {
    private ObjAIBase      _owner;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true,
        TriggersSpellCasts = true
    };

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _owner  = owner;
        _target = target;
    }
}

public class TaricCritAttack : ISpellScript {
    private ObjAIBase      _owner;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true,
        TriggersSpellCasts = true
    };

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _owner  = owner;
        _target = target;
    }
}