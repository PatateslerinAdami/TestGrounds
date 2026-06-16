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

public class EnrageBuff : IBuffGameScript
{
    private ObjAIBase _oldion;
    private Spell _spell;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _oldion = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        int level = ownerSpell.CastInfo.SpellLevel;
        float[] ad = { 0, 25f, 35f, 45f, 55f, 65f };
        StatsModifier.AttackDamage.FlatBonus = ad[Math.Clamp(level, 0, 5)];
        unit.AddStatModifier(StatsModifier);
        ApiEventManager.OnHitUnit.AddListener(this, _oldion, OnHitUnit, false);
        ApiEventManager.OnKill.AddListener(this, _oldion, OnKill, false);
    }

    private void OnHitUnit(DamageData data)
    {
        if (_oldion == null) return;
        int level = _oldion.Stats.Level;
        float cost = 6f + level;
        if (_oldion.Stats.CurrentHealth > 20)
            _oldion.TakeDamage(_oldion, cost, DamageType.DAMAGE_TYPE_TRUE,
                DamageSource.DAMAGE_SOURCE_DEFAULT, false);
    }

    private void OnKill(DeathData data)
    {
        if (_oldion == null) return;
        AddBuff("EnrageBuffMaxHP", 25000f, 1, _spell, _oldion, _oldion, true);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        unit.RemoveStatModifier(StatsModifier);
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}
