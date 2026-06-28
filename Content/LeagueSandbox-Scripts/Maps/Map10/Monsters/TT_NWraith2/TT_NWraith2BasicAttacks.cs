using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;

public class TT_NWraith2BasicAttack: ISpellScript {

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };
}

public class TT_NWraith2BasicAttack2: ISpellScript {

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };
}

public class TT_NWraith2BasicAttack3: ISpellScript {

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };
}

