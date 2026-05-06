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

internal class Blind : IBuffGameScript {
    private Particle _blind;
    
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.BLIND,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; }

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _blind = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "LOC_Blind ", unit, buff.Duration, bone: "head");
        ApiEventManager.OnPreDealDamage.AddListener(this, unit, OnPreDealDamage);
    }

    private void OnPreDealDamage(DamageData data)
    {
        if (!data.IsAutoAttack) return;
        data.PostMitigationDamage = 0;
        data.DamageResultType = DamageResultType.RESULT_MISS;
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { RemoveParticle(_blind); }
}

