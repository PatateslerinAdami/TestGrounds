using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;


namespace Spells;

public class MordekaiserBasicAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}

public class MordekaiserBasicAttack2 : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}

public class MordekaiserCritAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        IsDamagingSpell = true
    };
}