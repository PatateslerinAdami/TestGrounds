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

    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = true,
        TriggersSpellCasts   = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _alistar = owner;
    }

    public void OnSpellPostCast(Spell spell) {
        
        SpellEffectCreate("Meditate_eff.troy", _alistar, _alistar, _alistar, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
        
        var ap         = _alistar.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var healAmount = spell.SpellData.EffectLevelAmount[2][spell.CastInfo.SpellLevel] + ap;
        var alliesInRange = GetUnitsInRange(_alistar, _alistar.Position, 575, true, SpellDataFlags.AffectFriends | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.NotAffectSelf);
        _alistar.TakeHeal(_alistar, healAmount, HealType.SelfHeal);
        
        foreach (var ally in alliesInRange) {
            SpellEffectCreate("Meditate_eff.troy", _alistar, ally, ally, scale: 1f, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
            ally.TakeHeal(_alistar, healAmount * 0.33f, HealType.IncomingHeal);
        }
    }
}