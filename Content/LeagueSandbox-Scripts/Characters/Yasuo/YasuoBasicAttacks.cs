using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells;

public class YasuoBasicAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        },
        IsDamagingSpell = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true
    };
}

public class YasuoBasicAttack2 : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        },
        IsDamagingSpell = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true
    };
}

public class YasuoCritAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        },
        IsDamagingSpell = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true
    };
}