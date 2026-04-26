using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptJax : ICharScript {
    private ObjAIBase _jax;
    private Spell     _spell;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jax   = owner;
        _spell = spell;
        ApiEventManager.OnHitUnit.AddListener(this, _jax, OnHit);
    }

    private void OnHit(DamageData data) {
        AddBuff("JaxRelentlessAssaultAS", 2.5f, 1, _spell, _jax, _jax);
    }
}