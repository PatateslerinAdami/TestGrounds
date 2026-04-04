using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class JudicatorRighteousFury : ISpellScript {
    private ObjAIBase      _kayle;
    private Spell          _spell;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _kayle = owner;
        _spell = spell;
    }

    public void OnSpellPostCast(Spell spell) {
        AddBuff("JudicatorRighteousFury", 10f, 1, _spell, _kayle, _kayle);
    }
}

public class JudicatorRighteousFuryAttack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true
    };
}
public class JudicatorRighteousFuryAttack2 : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true
    };
}