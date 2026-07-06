using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class Blind : IBuffGameScript {
    private Particle _blind;
    
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.BLIND,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        // Blind = guaranteed miss: raise the unit's miss chance to 100%. The engine rolls
        // it per auto attack (ObjAIBase.RollAutoAttackMiss) → HIT_Miss → 0 damage, no on-hit.
        // Auto-removed on deactivate by Buff.DeactivateBuff. Mirrors Riot's IncFlatMissChanceMod(1.0).
        StatsModifier.MissChance.FlatBonus = 1.0f;
        unit.AddStatModifier(StatsModifier);
        _blind = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "LOC_Blind ", unit, buff.Duration, bone: "head");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { RemoveParticle(_blind); }
}

