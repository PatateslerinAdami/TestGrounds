using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells;

public class ZedBasicAttack : ISpellScript {
    private ObjAIBase      _owner;

    public SpellScriptMetadata ScriptMetadata => new() {
        
        IsDamagingSpell      = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
    }
}

public class ZedBasicAttack2 : ISpellScript {
    private ObjAIBase      _owner;

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
    }
}

public class ZedCritAttack : ISpellScript {
    private ObjAIBase      _owner;

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
    }
}