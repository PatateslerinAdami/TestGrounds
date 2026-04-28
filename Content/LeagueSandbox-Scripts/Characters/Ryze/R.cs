using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class desperatepower : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { }
    

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var duration = 5f + 1 * (spell.CastInfo.SpellLevel - 1);
        AddBuff("DesperatePower", duration, 1, spell, owner, owner);
    }
}

public class ryzedesperatepowerattack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
    };
}

