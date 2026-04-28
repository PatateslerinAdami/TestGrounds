using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class IreliaHitenStyle : IBuffGameScript {
    private float     _heal;
    private ObjAIBase _irelia;
    private Particle  _passiveGlow, _passive;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HEAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _irelia   = ownerSpell.CastInfo.Owner;
        _passive = AddParticle(unit, unit, "irelia_hitenStlye_passive", unit.Position, bone: "BUFFBONE_GLB_WEAPON_1");
        _passiveGlow = AddParticle(unit,                          unit, "irelia_hitenStlye_passive_glow", unit.Position,
                                   bone: "BUFFBONE_GLB_WEAPON_1", lifetime: buff.Duration);
        unit.SetAnimStates(new Dictionary<string, string> {
            { "attack1", "Attack1b" },
            { "attack2", "Attack2b" },
            { "crit",    "Critb"    },
            { "idle1",   "Idle1b"   },
            { "run",     "Runb"     }
        });
        ApiEventManager.OnHitUnit.AddListener(this, _irelia, Heal);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_passive);
        RemoveParticle(_passiveGlow);
        unit.SetAnimStates(new Dictionary<string, string> {
            { "attack1", "" },
            { "attack2", "" },
            { "crit",    "" },
            { "idle1",   "" },
            { "run",     "" }
        });
        /*AddParticleTarget(_irelia, _irelia, "irelia_hitenStlye_passive_glow_end", _irelia, bone: "BUFFBONE_GLB_WEAPON_1",
                          lifetime: 0.25f);*/
    }

    private void Heal(DamageData data) {
        var wSpell = _irelia.GetSpell("IreliaHitenStyle").CastInfo.SpellLevel;
        _heal = 3 + 3 * (wSpell - 1);

        _irelia.Stats.CurrentHealth += _heal;
    }
}
