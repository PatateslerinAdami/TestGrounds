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

public class TriumphantRoar : ISpellScript {
    private ObjAIBase      _alistar;
    private Spell          _spell;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _alistar = owner;
        _spell = spell;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
    }

    public void OnSpellCast(Spell spell) {
        
    }

    public void OnSpellPostCast(Spell spell) {
        var alliesInRange = GetUnitsInRange(_alistar, _alistar.Position, 575, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AlwaysSelf);
        foreach (var ally in alliesInRange) {
            AddBuff("Triumphant_Roar", 1f, 1, _spell, ally, _alistar);
        }
    }
}