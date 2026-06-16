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

internal class PoppyDiplomaticImmunityDmg : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; private set; } = new();
    private ObjAIBase _owner;
    private float _ampMultiplier;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _owner = ownerSpell.CastInfo.Owner;

        // Spell data: Effect2 = 20/30/40% damage amp vs marked target
        float[] ampValues = { 1.20f, 1.30f, 1.40f };
        _ampMultiplier = ampValues[ownerSpell.CastInfo.SpellLevel - 1];

        AddParticleTarget(_owner, _owner, "DiplomaticImmunity_buf.troy", _owner, buff.Duration);

        // Immunity: zero damage from attackers without PoppyDITarget
        ApiEventManager.OnPreTakeDamage.AddListener(this, unit, OnPreTakeDamage, false);

        // Damage amp: amplify damage dealt to target with PoppyDITarget
        ApiEventManager.OnPreDealDamage.AddListener(this, _owner, OnPreDealDamage, false);
    }

    public void OnPreTakeDamage(DamageData damage)
    {
        // If the attacker is dead/null or doesn't have PoppyDITarget, Poppy is immune
        // NOTE: Must set PostMitigationDamage, not Damage — the pipeline computes
        // PostMitigationDamage from Damage BEFORE this event fires, then uses
        // PostMitigationDamage after. Same pattern as Pantheon/Aatrox/Jax.
        if (damage.Attacker == null || damage.Attacker.IsDead || !damage.Attacker.HasBuff("PoppyDITarget"))
        {
            damage.PostMitigationDamage = 0f;
        }
    }

    public void OnPreDealDamage(DamageData damage)
    {
        // Amplify damage Poppy deals to the marked target (with PoppyDITarget)
        if (damage.Target.HasBuff("PoppyDITarget"))
        {
            // Must modify PostMitigationDamage — Damage was already consumed by the pipeline.
            var bonus = damage.PostMitigationDamage * (_ampMultiplier - 1.0f);
            damage.PostMitigationDamage += bonus;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.OnPreTakeDamage.RemoveListener(this, unit);
        ApiEventManager.OnPreDealDamage.RemoveListener(this, _owner);
    }
}
