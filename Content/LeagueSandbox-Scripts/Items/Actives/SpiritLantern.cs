using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace ItemSpells;

public class SpiritLantern : ISpellScript {
    private       ObjAIBase _owner;
    private       Minion    _ward;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        
        var cursor   = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
        var current  = new Vector2(owner.Position.X,                owner.Position.Y);
        var distance = cursor - current;
        
        float   duration = 180f;
        
        Vector2 truecoords;
        if (distance.Length() > 500f) {
            distance = Vector2.Normalize(distance);
            var range = distance * 500f;
            truecoords = current + range;
        } else { truecoords = cursor; }

        _ward = AddMinion(owner, "YellowTrinket", "YellowTrinket", truecoords, owner.Team, 0, true, true, true);
        _ward.Stats.ManaPoints.BaseValue = duration;
        _ward.Stats.CurrentMana          = duration;
        AddParticle(owner, _ward, "TrinketOrbLvl1Audio", truecoords);
        if (owner.HasBuff("YellowTrinketTracker")) {
            var buff = owner.GetBuffWithName("YellowTrinketTracker").BuffScript as YellowTrinketTracker;
            buff?.AddWard(_ward);
        } else {
            var buff = AddBuff("YellowTrinketTracker", 25000f, 1, spell, owner, owner, true).BuffScript as YellowTrinketTracker;
            buff?.AddWard(_ward);
        }
    }
}