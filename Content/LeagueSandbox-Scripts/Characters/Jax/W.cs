using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class JaxEmpowerTwo : ISpellScript {
    private ObjAIBase      _jax;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jax = owner;
    }

    public void OnSpellPostCast(Spell spell) {
        AddBuff("JaxEmpowerTwo", 6f, 1, spell, _jax, _jax);
    }
}

public class JaxEmpowerAttack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = false,
        IsDamagingSpell = true
    };
}

