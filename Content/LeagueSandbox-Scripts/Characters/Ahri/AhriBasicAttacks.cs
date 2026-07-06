using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AhriBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };
}

public class AhriBasicAttack2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };
}

public class AhriCritAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        },
    };

}