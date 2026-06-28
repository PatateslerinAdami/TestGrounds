using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SivirQ : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {

    }
}

public class SivirQMissile : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {

    }
}

public class SivirQMissileReturn : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {

    }
}

public class SivirQMissileReturnDead : ISpellScript {

    public SpellScriptMetadata ScriptMetadata => new() {
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {

    }
}