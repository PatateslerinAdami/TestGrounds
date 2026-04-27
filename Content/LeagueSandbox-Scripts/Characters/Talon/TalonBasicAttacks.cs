using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TalonBasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true,
        CastingBreaksStealth = true
    };
}

public class TalonBasicAttack2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true,
        CastingBreaksStealth = true
    };
}

public class TalonCritAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true,
        CastingBreaksStealth = true
    };
}