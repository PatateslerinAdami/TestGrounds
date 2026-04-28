using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class VayneInquisition : ISpellScript {
    private ObjAIBase _vayne;
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _vayne = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var duration = 0f;
        duration += spell.CastInfo.SpellLevel switch {
            3 => 12f,
            2 => 10f,
            _ => 8f
        };
        AddBuff("VayneInquisition",  duration, 1, spell, _vayne, _vayne);
    }
}

public class VayneUltAttack : ISpellScript {
    private ObjAIBase _vayne;
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _vayne = owner;
    }
}