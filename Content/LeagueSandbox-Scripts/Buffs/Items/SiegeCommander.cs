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

internal class SiegeCommander : IBuffGameScript {
    private const float TowerDamageBonusPct = 0.15f;
    private const float BonusMoveSpeedScale  = 0.75f;

    private ObjAIBase _owner;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _owner = buff.SourceUnit;
        if (_owner != null && unit is LaneMinion) {
            var ownerBonusMoveSpeed = _owner.Stats.GetTrueMoveSpeed() - _owner.Stats.MoveSpeed.BaseValue;
            if (ownerBonusMoveSpeed < 0f) ownerBonusMoveSpeed = 0f;

            StatsModifier.MoveSpeed.FlatBonus = ownerBonusMoveSpeed * BonusMoveSpeedScale;
            unit.AddStatModifier(StatsModifier);
        }

        ApiEventManager.OnDealDamage.AddListener(this, unit, OnDealDamage);
    }

    private void OnDealDamage(DamageData data) {
        if (_owner == null) return;
        if (!IsValidTarget(_owner, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectTurrets)) return;

        data.PostMitigationDamage +=
            data.Target.Stats.GetPostMitigationDamage(data.Damage * TowerDamageBonusPct, data.DamageType, data.Attacker);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}
