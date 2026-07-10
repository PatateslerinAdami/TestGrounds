using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class FerociousHowl : ISpellScript {
    private ObjAIBase      _alistar;

    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = true,
        TriggersSpellCasts   = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _alistar = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        AddBuff("FerociousHowl", 7f, 1, spell, _alistar, _alistar);
    }

    public void OnSpellCast(Spell spell) {
        
    }

    public void OnSpellPostCast(Spell spell) {
    }
}