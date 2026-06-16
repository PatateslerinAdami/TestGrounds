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

public class CharScriptSightWard : ICharScript
{
    private ObjAIBase _owner;
    private bool _isDying;
    private PeriodicTicker _manaDrainTicker;
    private Particle _particle;
    private Region _bubbleRegion;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell = null)
    {
        _owner = owner;
        _isDying = false;
        _manaDrainTicker.Reset();
        _owner.SetStatus(StatusFlags.Ghosted, true);
        _owner.SetCollisionRadius(0.0f);
        _bubbleRegion = AddPosPerceptionBubble(_owner.Position, 900f, _owner.Stats.ManaPoints.Total, _owner.Team);
        _particle = AddParticleTarget(owner, owner, "Global_Trinket_MiniYellow", owner, -1f);
        ApiEventManager.OnDeath.AddListener(this, owner, OnOwnerDeath);
    }

    public void OnUpdate(float diff)
    {
        if (_owner.Stats.CurrentMana <= 0 && !_isDying)
        {
            KillWard();
            return;
        }

        var manaTicks = _manaDrainTicker.ConsumeTicks(diff, 1000f, false, 1,
            maxTotalTicks: (int)_owner.Stats.ManaPoints.Total);
        if (manaTicks == 1)
        {
            SpendPAR(_owner, manaTicks);
        }
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell = null)
    {
        ApiEventManager.OnDeath.RemoveListener(this, owner, OnOwnerDeath);
        CleanupBubble();
    }

    private void OnOwnerDeath(DeathData data)
    {
        CleanupBubble();
    }

    private void CleanupBubble()
    {
        if (_bubbleRegion != null)
        {
            _bubbleRegion.SetToRemove();
            _bubbleRegion = null;
        }
    }

    private void KillWard()
    {
        if (_isDying || _owner.IsDead) return;
        _isDying = true;

        ApiEventManager.OnDeath.RemoveListener(this, _owner, OnOwnerDeath);

        if (_particle != null)
        {
            RemoveParticle(_particle);
            _particle = null;
        }
        AddParticleTarget(_owner, _owner, "Ward_Green_Death", _owner);
        _owner.TakeDamage(_owner, 5000f, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_RAW, DamageResultType.RESULT_NORMAL);
        CleanupBubble();
    }
}

