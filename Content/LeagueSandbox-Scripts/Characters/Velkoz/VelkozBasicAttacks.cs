using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;


namespace Spells;

public class VelkozBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class VelkozBasicAttack2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class VelkozBasicAttack3 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class VelkozCritAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}