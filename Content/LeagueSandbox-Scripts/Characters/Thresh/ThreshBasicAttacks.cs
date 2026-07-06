using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;


namespace Spells;
//TODO: something is weird with thresh autoattacks  no hit fx weird spell names and no dmg
public class ThreshBasicAttack1SFast : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class ThreshBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class ThreshBasicAttack2: ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class ThreshBasicAttack1L: ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class ThreshBasicAttack1M: ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class ThreshBasicAttack1S: ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class ThreshBasicAttack2L: ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class ThreshBasicAttack2M: ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class ThreshBasicAttack2S: ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}

public class ThreshCritAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}