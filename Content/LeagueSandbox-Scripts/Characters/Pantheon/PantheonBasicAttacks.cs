using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells;

public class PantheonBasicAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}

public class PantheonBasicAttack2 : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}

public class PantheonCritAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}