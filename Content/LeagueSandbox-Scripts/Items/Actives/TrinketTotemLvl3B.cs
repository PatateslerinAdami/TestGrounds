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

public class TrinketTotemLvl3B : ISpellScript {
    private Minion         _ward;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = false
    };
    
    public void OnActivate(ObjAIBase owner, Spell spell) {
        if (GameTime() <= 30000) {
            spell.SetCooldown(30f - GameTime()/1000, true);
        }
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        
        var cursor   = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
        var current  = new Vector2(owner.Position.X,                owner.Position.Y);
        var distance = cursor - current;
        
        Vector2 truecoords;
        if (distance.Length() > 500f) {
            distance = Vector2.Normalize(distance);
            var range = distance * 500f;
            truecoords = current + range;
        } else { truecoords = cursor; }

        _ward = AddMinion(owner, "VisionWard", "VisionWard", truecoords, owner.Team, 0,true, true,true);
        AddParticle(owner, _ward, "TrinketOrbLvl1Audio", truecoords);
        AddParticle(owner, _ward, "Visionward", truecoords);
        if (owner.HasBuff("VisionWardTracker")) {
            var buff = owner.GetBuffWithName("VisionWardTracker").BuffScript as VisionWardTracker;
            buff?.AddWard(_ward);
        } else {
            var buff = AddBuff("VisionWardTracker", 25000f, 1, spell, owner, owner, true).BuffScript as VisionWardTracker;
            buff?.AddWard(_ward);
        }
    }
}