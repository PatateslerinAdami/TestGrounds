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
using GameServerLib.GameObjects.AttackableUnits;

namespace Spells;
//Purple 2nd Lane Turret
public class ChaosTurretWorm2BasicAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true,
        TriggersSpellCasts = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
    };
}