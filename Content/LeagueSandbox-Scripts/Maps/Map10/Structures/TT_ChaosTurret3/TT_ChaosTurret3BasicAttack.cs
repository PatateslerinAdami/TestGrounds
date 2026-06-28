using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;

public class TT_ChaosTurret3BasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell    = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };
}