using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;

public class DragonBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        
        MissileParameters  = new MissileParameters
        {
            Type = MissileType.Target
        },
        IsDamagingSpell    = true
    };
}