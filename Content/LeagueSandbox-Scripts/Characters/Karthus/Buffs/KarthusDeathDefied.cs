using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

// Karthus "Death Defied" passive. Faithful to S1 DeathDefied.lua:
//   BuffOnDeath  -> if (Owner is hero) BecomeZombie = true   (death becomes a ZOMBIE, not a removal)
//   BuffOnZombie -> AddBuff "DeathDefiedBuff" (7s)           (the keep-casting window)
// Exercises the engine zombie pipeline: the OnDeath listener arms DeathData.BecomeZombie before
// Die() reads it; the OnZombie listener fires once the unit has entered the zombie state.
internal class KarthusDeathDefied : IBuffGameScript {
    private AttackableUnit _unit;
    private Spell _ownerSpell;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.INTERNAL,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        PersistsThroughDeath = true,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit = unit;
        _ownerSpell = ownerSpell;
        ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
        ApiEventManager.OnZombie.AddListener(this, unit, OnZombie);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.OnDeath.RemoveListener(this);
        ApiEventManager.OnZombie.RemoveListener(this);
    }

    // Runs inside Die() BEFORE the zombie-vs-death decision — arm the zombie path for hero deaths.
    private void OnDeath(DeathData data) {
        if (_unit is Champion) {
            data.BecomeZombie = true;
        }
    }

    // Fires once the death produced a zombie — grant the 7s keep-alive that ends the phase.
    private void OnZombie(AttackableUnit unit, DeathData data) {
        AddBuff("KarthusDeathDefiedBuff", 7f, 1, _ownerSpell, unit, unit as ObjAIBase);
    }
}
