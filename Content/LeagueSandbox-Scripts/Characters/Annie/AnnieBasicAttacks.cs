using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells;

public class AnnieBasicAttack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
    };
}

public class AnnieBasicAttack2 : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
    };
}

public class AnnieCritAttack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
    };
}