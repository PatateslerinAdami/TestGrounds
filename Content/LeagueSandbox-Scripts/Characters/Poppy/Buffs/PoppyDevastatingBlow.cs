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

internal class PoppyDevastatingBlow : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; private set; } = new();
    private Spell _spell;
    private ObjAIBase _owner;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _owner = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        AddParticleTarget(_owner, _owner, "Poppy_DevastatingBlow_buf.troy", _owner, 5f);
        _owner.CancelAutoAttack(true);
        ApiEventManager.OnHitUnit.AddListener(this, _owner, OnHit, true);
        _owner.SkipNextAutoAttack();
    }

    public void OnHit(DamageData damage)
    {
        var target = damage.Target;

        float rank = _spell.CastInfo.SpellLevel;
        float baseFlat = 20 + rank * 20; // 20/40/60/80/100
        float totalAd = _owner.Stats.AttackDamage.Total;
        float targetMaxHp = target.Stats.HealthPoints.Total;
        float percentHp = targetMaxHp * 0.08f;
        float ap = _owner.Stats.AbilityPower.Total;

        // Cap %HP damage: 75/150/225/300/375
        float[] maxBonus = { 75, 150, 225, 300, 375 };
        float maxBonusDmg = maxBonus[(int)rank - 1];
        if (percentHp > maxBonusDmg)
            percentHp = maxBonusDmg;

        // Spell data: Coefficient=0.6 (60% total AD), Coefficient2=1 (100% AP)
        float totalDamage = baseFlat + totalAd * 0.6f + ap + percentHp;

        target.TakeDamage(_owner, totalDamage, DamageType.DAMAGE_TYPE_MAGICAL,
            DamageSource.DAMAGE_SOURCE_ATTACK, false);
        AddParticleTarget(_owner, target, "Poppy_DevastatingBlow_tar.troy", target, 0.5f);

        _owner.RemoveBuffsWithName("PoppyDevastatingBlow");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.OnHitUnit.RemoveListener(this, _owner);
    }
}
