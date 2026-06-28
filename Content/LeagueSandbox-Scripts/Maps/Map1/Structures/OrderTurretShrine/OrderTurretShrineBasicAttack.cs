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
using GameServerLib.GameObjects.AttackableUnits;

namespace Spells;
//Purple Fountain Turret (Laser)
public class OrderTurretShrineBasicAttack : ISpellScript {

    private ObjAIBase      _turret;

    public SpellScriptMetadata ScriptMetadata => new() {
        IsDamagingSpell    = true,
        TriggersSpellCasts = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _turret = owner;
        ApiEventManager.OnHitUnit.AddListener(this, _turret, OnHit);
    }

    private void OnHit(DamageData data) {
    }
}