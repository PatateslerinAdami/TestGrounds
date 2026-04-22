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

namespace Buffs;

internal class MordekaiserCOTGPetBuff : IBuffGameScript {
    private ObjAIBase _mordekaiser;
    private Buff _buff;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        IsHidden =  true,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _mordekaiser = ownerSpell.CastInfo.Owner;
        _buff = buff;
        ApiEventManager.OnDeath.AddListener(this, unit, OnGhostDeath, true);
        //Stats applied here might not be notified to the clients, even though all necessary packets are sent, i was unsuccessful on pinpointing the cause (cabeca143).
        if (unit is not Pet pet) return;
        StatsModifier.AbilityPower.FlatBonus = _mordekaiser.Stats.AbilityPower.Total * 0.75f;
        StatsModifier.AttackDamage.FlatBonus = _mordekaiser.Stats.AttackDamage.Total * 0.75f;
        StatsModifier.HealthPoints.FlatBonus = _mordekaiser.Stats.HealthPoints.Total * 0.15f;

        while (pet.Stats.Level < pet.ClonedUnit.Stats.Level) pet.LevelUp();
        pet.Stats.AttackDamage.BaseValue = pet.ClonedUnit.CharData.BaseDamage;

        pet.AddStatModifier(StatsModifier);
        pet.Stats.CurrentHealth = pet.Stats.HealthPoints.Total;
    }
    
    public void OnGhostDeath(DeathData data) { _buff.DeactivateBuff(); }
}