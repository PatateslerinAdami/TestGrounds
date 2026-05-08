using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;


namespace ItemPassives;

public class ItemID_1500 : IItemScript
{
    private const float ResetAfterMs = 5000f;
    private const float InitialBonusMultiplier = 0.375f;
    private const float EarlyBonusStep = 0.375f;
    private const float LateBonusStep = 0.25f;
    private const float LateBonusThreshold = 0.75f;
    private const float MaxBonusMultiplier = 1.25f;

    private ObjAIBase _turret;
    private float _currentModifier = 0f;
    private float _elapsedMs = 0f;
    private bool _timerEnabled = false;
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner)
    {
        _turret = owner;
        ResetState();
        ApiEventManager.OnPreDealDamage.AddListener(this, _turret, OnPreDealDamage);
    }

    private void OnPreDealDamage(DamageData data)
    {
        if (data.Target is not Champion || data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;

        if (data.DamageResultType == DamageResultType.RESULT_DODGE)
        {
            data.DamageResultType = DamageResultType.RESULT_NORMAL;
        }

        if (_timerEnabled)
        {
            var bonusPreMitigationDamage = data.Damage * _currentModifier;
            data.PostMitigationDamage += data.Target.Stats.GetPostMitigationDamage(bonusPreMitigationDamage,
                DamageType.DAMAGE_TYPE_PHYSICAL,
                data.Attacker);
            _elapsedMs = 0f;
            AdvanceModifier();
        }
        else
        {
            _elapsedMs = 0f;
            _timerEnabled = true;
            _currentModifier = InitialBonusMultiplier;
        }
    }

    public void OnUpdate(float diff)
    {
        if (!_timerEnabled) return;

        _elapsedMs += diff;
        if (_elapsedMs < ResetAfterMs) return;

        ResetState();
    }

    private void AdvanceModifier()
    {
        if (_currentModifier >= MaxBonusMultiplier) return;

        _currentModifier += _currentModifier < LateBonusThreshold ? EarlyBonusStep : LateBonusStep;
        if (_currentModifier > MaxBonusMultiplier)
        {
            _currentModifier = MaxBonusMultiplier;
        }
    }

    private void ResetState()
    {
        _currentModifier = 0f;
        _elapsedMs = 0f;
        _timerEnabled = false;
    }

    public void OnDeactivate(ObjAIBase owner)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
        ResetState();
    }
}