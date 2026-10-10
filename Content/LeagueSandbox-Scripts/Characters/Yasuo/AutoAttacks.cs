using System.Numerics;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Spells
{
    public class YasuoBasicAttack : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true,
            CastingBreaksStealth = true
        };

        public void OnSpellCast(Spell spell)
        {
            YasuoCastHelpers.ResetSheath(spell);
        }
    }

    public class YasuoBasicAttack2 : YasuoBasicAttack
    {
    }

    public class YasuoBasicAttack3 : YasuoBasicAttack
    {
    }

    public class YasuoBasicAttack4 : YasuoBasicAttack
    {
    }

    public class YasuoBasicAttack5 : YasuoBasicAttack
    {
    }

    public class YasuoBasicAttack6 : YasuoBasicAttack
    {
    }

    public class YasuoBasicAttack7 : YasuoBasicAttack
    {
    }

    public class YasuoBasicAttack8 : YasuoBasicAttack
    {
    }

    public class YasuoBasicAttack9 : YasuoBasicAttack
    {
    }

    public class YasuoCritAttack : YasuoBasicAttack
    {
    }

    public class YasuoCritAttack2 : YasuoBasicAttack
    {
    }

    public class YasuoCritAttack3 : YasuoBasicAttack
    {
    }

    public class YasuoCritAttack4 : YasuoBasicAttack
    {
    }

    public class YasuoCritAttack5 : YasuoBasicAttack
    {
    }
}
