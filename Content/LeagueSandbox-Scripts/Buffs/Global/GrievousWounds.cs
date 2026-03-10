using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class GrievousWounds : IBuffGameScript {
    private const float HealMultiplier = 0.4f;
    private Particle _grievousWoundsOverheadParticle;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; }

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.OnReceiveHeal.AddListener(this, unit, OnTakeHeal);
        _grievousWoundsOverheadParticle = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "global_grievousWound_tar", unit,
            buff.Duration,             bone: "C_BUFFBONE_GLB_OVERHEAD_LOC");
    }

    private void OnTakeHeal(HealData healData) {
        if (healData.HealAmount <= 0.0f) return;
        healData.HealAmount *= HealMultiplier;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_grievousWoundsOverheadParticle);
    }
}