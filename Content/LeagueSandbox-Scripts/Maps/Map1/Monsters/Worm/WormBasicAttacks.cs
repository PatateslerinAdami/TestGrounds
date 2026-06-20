using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;
//TODO: Verify if ranged or melee
public class WormBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters  = new MissileParameters
        {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true,
    };
}

public class WormAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters  = new MissileParameters { Type = MissileType.Target },
        IsDamagingSpell    = true,
    };
}