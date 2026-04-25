using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;


namespace Spells;

public class NamiBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class NamiBasicAttack2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class NamiCritAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}