using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells;

public class KayleBasicAttack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true
    };
}

public class KayleBasicAttack2 : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true
    };
}

public class KayleCritAttack : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell      = true
    };
}