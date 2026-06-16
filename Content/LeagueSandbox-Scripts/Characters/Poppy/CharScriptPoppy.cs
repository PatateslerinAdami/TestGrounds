using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptPoppy : ICharScript
{
    public StatsModifier StatsModifier { get; private set; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell = null)
    {
        // Valiant Fighter passive - damage reduction
        ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnPreTakeDamage, false);

        // Paragon of Demacia passive - stacking armor on hit
        ApiEventManager.OnHitUnit.AddListener(this, owner, OnPoppyHit, false);
    }

    public void OnPreTakeDamage(DamageData damage)
    {
        var target = damage.Target;
        if (target == null) return;

        float currentHp = target.Stats.CurrentHealth;
        float damageAmount = damage.Damage;

        // Valiant Fighter: Any damage exceeding 10% of current HP is reduced by 50%
        float softCap = currentHp * 0.10f;

        if (damageAmount > softCap)
        {
            float excess = damageAmount - softCap;
            float reducedExcess = excess * 0.5f;
            damage.Damage = softCap + reducedExcess;
        }
    }

    public void OnPoppyHit(DamageData damage)
    {
        // Poppy attacked someone — gain a Paragon stack (icon + armor + AD)
        var poppy = damage.Attacker as ObjAIBase;
        if (poppy == null) return;

        // PoppyParagonStats handles both the visible icon (via PoppyParagonStats.luaobj → BuffTextureName = PoppyDefenseOfDemacia.dds)
        // AND per-stack stats via STACKS_AND_RENEWS delta pattern
        AddBuff("PoppyParagonStats", 5f, 1, null, poppy, poppy, false);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell = null)
    {
        ApiEventManager.OnPreTakeDamage.RemoveListener(this, owner);
        ApiEventManager.OnHitUnit.RemoveListener(this, owner);
    }
}
