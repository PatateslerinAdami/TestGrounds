using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class InfernalGuardianBurning : IBuffGameScript {
    private       ObjAIBase _annie;
    private       Spell     _spell;
    private       Pet       _pet;
    private       float     _timer  = 0f;
    private       float     _summonCooldown;   // R's full summon cooldown, captured at cast (see OnActivate)
    private const float     MaxTime = 1000f;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.COMBAT_ENCHANCER
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _annie = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        // ownerSpell is the InfernalGuardian (summon) spell. R.cs zeroed the guide spell's cooldown so
        // pet-steering works, which means the 120s summon cooldown no longer lives on any slot. Capture
        // it here (full, CDR already applied by GetCooldown) so OnDeactivate can restore the REMAINING
        // amount to R when the guide swaps back to the summon.
        _summonCooldown = ownerSpell.GetCooldown();
        if (unit is not Pet pet) return;
        _pet = pet;

        StatsModifier.Armor.FlatBonus        = 20.0f * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.MagicResist.FlatBonus  = 20f   * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.AttackDamage.FlatBonus = 20f   * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.HealthPoints.FlatBonus = 900f  * (ownerSpell.CastInfo.SpellLevel - 1);
        pet.AddStatModifier(StatsModifier);
        pet.Stats.CurrentHealth = pet.Stats.HealthPoints.Total;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        unit.Die(CreateDeathData(false,                                  0, unit, unit, DamageType.DAMAGE_TYPE_TRUE,
                                 DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
        RemoveBuff(buff.SourceUnit, "InfernalGuardianTimer");
        // Tibbers has ended (killed or the 45s elapsed) — swap Annie's R (slot 3) from the guide spell
        // back to the summon so she can re-cast Tibbers. Without this, R stayed stuck on
        // InfernalGuardianGuide after the first cast, so Tibbers could never be re-summoned once it died.
        var resummon = SetSpell(_annie, "InfernalGuardian", SpellSlotType.SpellSlots, 3);

        // Restore the REMAINING summon cooldown to R: the 120s started at cast, and Tibbers has now been
        // alive for buff.TimeElapsed seconds. Without this, the swap-back would inherit the guide's
        // zeroed cooldown (ObjAIBase.SetSpell copies CurrentCooldown) and let Annie re-summon instantly.
        // Tibbers lives at most 45s while the summon cooldown is 120s, so the remainder is always > 0.
        float remaining = _summonCooldown - buff.TimeElapsed;
        if (resummon != null && remaining > 0f) {
            resummon.SetCooldown(remaining, true);
        }
    }

    public void OnUpdate(Buff buff, float diff) {
        _timer += diff;
        if (!(_timer >= MaxTime)) return;
        if (_pet == null) return;
        var enemiesInRange = GetUnitsInRange(_annie, _pet.Position, 350f, true,
                                             SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                             SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        var ap          = _annie.Stats.AbilityPower.Total * _spell.SpellData.Coefficient2;
        var totalDamage = 35.0f + ap;
        foreach (var enemy in enemiesInRange) {
            enemy.TakeDamage(_pet.Owner, totalDamage, DamageType.DAMAGE_TYPE_MAGICAL,
                             DamageSource.DAMAGE_SOURCE_PET, false);
        }

        _timer = 0f;
    }
}