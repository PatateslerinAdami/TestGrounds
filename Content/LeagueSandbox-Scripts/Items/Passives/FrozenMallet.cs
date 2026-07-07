using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemID_3022 : IItemScript
{
    private const float SlowDuration = 1.5f;
    private const float MeleeSlowPercent = 0.40f;
    private const float RangedSlowPercent = 0.30f;

    private const string InternalSlowBuffName = "ItemInternalSlow";
    private const string VisualSlowBuffName = "ItemSlow";

    private ObjAIBase _owner;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner)
    {
        _owner = owner;
        ApiEventManager.OnHitUnit.AddListener(this, owner, OnHitUnit, false);
        owner.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(ObjAIBase owner)
    {
        ApiEventManager.OnHitUnit.RemoveListener(this);
        _owner = null;
    }

    public void OnUpdate(float diff)
    {
    }

    private void OnHitUnit(DamageData data)
    {
        if (!ShouldApplyFrozenMallet(data))
        {
            return;
        }

        var slowPercent = _owner.IsMelee ? MeleeSlowPercent : RangedSlowPercent;

        var variables = new VariableTable();
        variables.Set("slowPercent", slowPercent);
        variables.Set("attackSpeedSlowAmount", 0.0f);

        ApplyItemInternalSlow(data.Target, slowPercent, variables);

        AddBuff(
            VisualSlowBuffName,
            SlowDuration,
            1,
            null,
            data.Target,
            _owner,
            variableTable: variables
        );
    }

    private bool ShouldApplyFrozenMallet(DamageData data)
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

        if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK)
        {
            return false;
        }

        if (data.PostMitigationDamage <= 0.0f)
        {
            return false;
        }

        if (data.Target is LaneTurret)
        {
            return false;
        }

        return true;
    }

    private void ApplyItemInternalSlow(AttackableUnit target, float slowPercent, VariableTable variables)
    {
        var normalizedNewSlow = slowPercent;
        if (normalizedNewSlow < 0.0f)
        {
            normalizedNewSlow = -normalizedNewSlow;
        }

        var existingSlows = target.GetBuffsWithName(InternalSlowBuffName);

        foreach (var existing in existingSlows)
        {
            if (existing.SourceUnit != _owner)
            {
                continue;
            }

            var existingSlow = existing.BuffVars.GetFloat("slowPercent", 0.0f);
            if (existingSlow < 0.0f)
            {
                existingSlow = -existingSlow;
            }

            if (normalizedNewSlow <= existingSlow)
            {
                existing.Refresh();
                return;
            }

            target.RemoveBuff(existing);
            break;
        }

        AddBuff(
            InternalSlowBuffName,
            SlowDuration,
            1,
            null,
            target,
            _owner,
            variableTable: variables
        );
    }
}
