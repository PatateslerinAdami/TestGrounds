using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptMordekaiser : ICharScript {
    private const float ShieldPerLevel = 30.0f;

    private ObjAIBase _mordekaiser;

    public void OnPostActivate(ObjAIBase owner, Spell spell) {
        _mordekaiser = owner;

        ApiEventManager.OnLevelUp.AddListener(this, owner, OnLevelUp);
        ApiEventManager.OnResurrect.AddListener(this, owner, OnResurrect);
        ApiEventManager.OnDeath.AddListener(this, owner, OnDeath);
        AddBuff("MorderkaiserIronMan", 25000f, 1, spell, _mordekaiser, _mordekaiser, true);
        _mordekaiser.SpendPAR(_mordekaiser.GetPAR());
    }

    private void OnDeath(DeathData data) {
        AddParticleTarget(_mordekaiser, _mordekaiser, "mordakaiser_death_01", _mordekaiser, bone: "chest");
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) {
        ApiEventManager.OnLevelUp.RemoveListener(this);
        ApiEventManager.OnResurrect.RemoveListener(this);
    }

    private void OnLevelUp(AttackableUnit unit) {
        _mordekaiser.Stats.ManaPoints.FlatBonus += ShieldPerLevel;
    }

    private void OnResurrect(ObjAIBase owner) {
        owner.SpendPAR(owner.GetPAR());
    }
}
