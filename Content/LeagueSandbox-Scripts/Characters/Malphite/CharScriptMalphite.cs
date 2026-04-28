using System.Linq;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptMalphite : ICharScript {
    private ObjAIBase _malphite;
    private Spell     _spell;
    private bool      _shieldInitialized;

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _malphite = owner;
        _spell    = spell;
        _shieldInitialized = false;

        ApiEventManager.OnUpdateStats.AddListener(this, _malphite, OnUpdateStats);
        ApiEventManager.OnTakeDamage.AddListener(this, _malphite, OnPreTakeDamage);

        TryInitializeShield();
    }

    private void OnPreTakeDamage(DamageData data) {
        _malphite.GetSpell("MalphiteShield").SetCooldown(10f, true);
        AddBuff("MalphiteShieldBeenHit", 10f, 1, _spell, _malphite, _malphite);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        TryInitializeShield();
    }

    private void TryInitializeShield() {
        if (_shieldInitialized || _malphite == null || _spell == null) return;
        if (!_malphite.VisibleForPlayers.Any()) return;

        if (!_malphite.HasBuff("MalphiteShieldEffect"))
            AddBuff("MalphiteShieldEffect", 25000f, 1, _spell, _malphite, _malphite, true);
        if (!_malphite.HasBuff("MalphiteShield"))
            AddBuff("MalphiteShield", 25000f, 1, _spell, _malphite, _malphite, true);

        _shieldInitialized = true;
        ApiEventManager.OnUpdateStats.RemoveListener(this, _malphite, OnUpdateStats);
    }
}
