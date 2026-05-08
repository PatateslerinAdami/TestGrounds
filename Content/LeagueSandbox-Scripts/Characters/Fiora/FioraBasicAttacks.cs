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

public class FioraBasicAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}

public class FioraBasicAttack2 : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}

public class FioraBasicAttackFast : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}

public class FioraCritAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}