using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells;

public class ryzebasicattack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
    };
}

public class ryzebasicattack2 : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
    };
}

public class ryzecritattack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
    };
}