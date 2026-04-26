using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptVisionWard : ICharScript {
    private ObjAIBase      _owner;
    private PeriodicTicker _periodicTicker1, _periodicTicker2;
    private bool           _enableRegenerationTimer;
    private Particle       _particle;
    private Region         _bubbleRegion;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner        = owner;
        _enableRegenerationTimer = false;
        _periodicTicker1.Reset();
        _periodicTicker2.Reset();
        _owner.SetStatus(StatusFlags.Ghosted, true);
        _owner.SetCollisionRadius(0.0f);
        _particle     = AddParticleTarget(owner, owner, "Ward_Vision_Idle", owner, -1f);
        _bubbleRegion = AddUnitPerceptionBubble(owner, 900f, -1, owner.Team, true);
        ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamge);
    }

    private void OnTakeDamge(DamageData data) {
        _enableRegenerationTimer = false;
        _periodicTicker2.Reset();
        _periodicTicker1.Reset();
    }

    public void OnUpdate(float diff) {
        if (!_enableRegenerationTimer) {
            var ticks2 = _periodicTicker2.ConsumeTicks(diff, 5000f, false, 1, maxTotalTicks: 1);
            if (ticks2 == 1) {
                _enableRegenerationTimer = true;
                _periodicTicker1.Reset();
            }
            return;
        }
        var ticks1 = _periodicTicker1.ConsumeTicks(diff, 3000f, false, 1);
        if (ticks1 != 1) return;
        if (_owner.Stats.CurrentHealth >= _owner.Stats.HealthPoints.Total) return;
        _owner.TakeHeal(_owner, 1f, HealType.SelfHeal);
        
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) {
        RemoveParticle(_particle);
        _bubbleRegion.SetToRemove();
    }
}