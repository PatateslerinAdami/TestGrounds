using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class UdyrBearStance : ISpellScript {
    Spell _spell;
    ObjAIBase _udyr;

    public StatsModifier StatsModifier { get; } = new();

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _spell = spell;
        _udyr = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        float duration = spell.CastInfo.SpellLevel switch {
            1 => 2f,
            2 => 2.25f,
            3 => 2.5f,
            4 => 2.75f,
            5 => 3f,
            _ => 2f
        };
        AddBuff("UdyrBearStance", 25000f, 1, spell, owner, owner, true);
        AddBuff("UdyrBearActivation", duration, 1, spell, owner, owner);
    }

    public void OnUpdate(float diff) {
    }
}

public class UdyrBearAttack : ISpellScript {
    private ObjAIBase _udyr;
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _udyr = owner;
    }
}
