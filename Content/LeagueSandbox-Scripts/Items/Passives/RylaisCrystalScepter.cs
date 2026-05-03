using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemID_3116 : IItemScript
{
    private const float SingleTargetSlowPercent = 0.35f;
    private const float AreaOrPeriodicSlowPercent = 0.15f;
    private const float SlowDuration = 1.5f;
    private const string SlowBuffName = "RylaiCrystalScepterSlow";

    private ObjAIBase _owner;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner)
    {
        _owner = owner;

        // Rylai triggers when the owner deals valid spell damage.
        ApiEventManager.OnDealDamage.AddListener(this, owner, OnDealDamage, false);
    }

    public void OnDeactivate(ObjAIBase owner)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
        _owner = null;
    }

    public void OnUpdate(float diff)
    {
    }

    private void OnDealDamage(DamageData data)
    {
        if (!ShouldApplyRylai(data))
        {
            return;
        }

        var slowPercent = GetSlowPercent(data.DamageSource);
        if (slowPercent <= 0.0f)
        {
            return;
        }

        var variables = new BuffVariables();
        variables.Set("slowPercent", slowPercent);

        AddBuff(
            SlowBuffName,
            SlowDuration,
            1,
            null,
            data.Target,
            _owner,
            buffVariables: variables
        );
    }

    private bool ShouldApplyRylai(DamageData data)
    {
        if (data == null || _owner == null)
        {
            return false;
        }

        if (data.Attacker != _owner)
        {
            return false;
        }

        if (data.Target == null || data.Target.IsDead)
        {
            return false;
        }

        if (data.Target.Team == _owner.Team)
        {
            return false;
        }

        if (data.IsAutoAttack)
        {
            return false;
        }

        if (data.PostMitigationDamage <= 0.0f)
        {
            return false;
        }

        // Do not slow towers/structures.
        if (data.Target is LaneTurret)
        {
            return false;
        }

        return true;
    }

    private float GetSlowPercent(DamageSource damageSource)
    {
        return damageSource switch
        {
            DamageSource.DAMAGE_SOURCE_SPELL => SingleTargetSlowPercent,

            DamageSource.DAMAGE_SOURCE_SPELLAOE
                or DamageSource.DAMAGE_SOURCE_PERIODIC
                or DamageSource.DAMAGE_SOURCE_SPELLPERSIST => AreaOrPeriodicSlowPercent,

            // Do not trigger on autos, procs, raw/internal damage, pets, death/reactive damage, etc.
            _ => 0.0f
        };
    }
}
