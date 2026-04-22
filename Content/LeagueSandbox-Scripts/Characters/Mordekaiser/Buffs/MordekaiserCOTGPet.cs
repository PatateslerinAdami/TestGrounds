using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiEventManager;

namespace Buffs;

internal class MordekaiserCOTGPet : IBuffGameScript {
    private ObjAIBase _mordekaiser;
    private Buff     _buff;
    private Particle _p;
    private Particle _p2;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff        = buff;
        _mordekaiser = ownerSpell.CastInfo.Owner;
        AddBuff("MordekaiserCOTGSelf", buff.Duration, 1, ownerSpell, _mordekaiser,
                _mordekaiser);
        AddBuff("MordekaiserCOTGPetBuff", buff.Duration, 1, ownerSpell, unit, _mordekaiser);
        ownerSpell.CastInfo.Owner.SetSpell("MordekaiserCotGGuide", 3, true);

        _p  = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "mordekaiser_cotg_ring", unit, buff.Duration);
        _p2 = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "mordekeiser_cotg_skin", unit, buff.Duration);

        OnDeath.AddListener(this, unit, OnGhostDeath, true);
        OnHitUnit.AddListener(this, unit as ObjAIBase, OnHit);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        //In theory the killer should be null, but that causes the ghost to not die(?)
        unit.Die(CreateDeathData(false,                                  0, unit, unit, DamageType.DAMAGE_TYPE_PHYSICAL,
                                 DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
        RemoveParticle(_p);
        RemoveParticle(_p2);

        RemoveBuff(ownerSpell.CastInfo.Owner, "MordekaiserCOTGSelf");
        var spell = ownerSpell.CastInfo.Owner.SetSpell("MordekaiserChildrenOfTheGrave", 3, true);
        //Check if this is done on-script or should be handled automatically
        spell.SetCooldown(spell.GetCooldown() - buff.TimeElapsed);
    }

    private void OnHit(DamageData data) {
        data.DamageType = DamageType.DAMAGE_TYPE_MAGICAL;
        data.PostMitigationDamage = data.Target.Stats.GetPostMitigationDamage(data.Damage, data.DamageType, data.Attacker);
    }

    public void OnGhostDeath(DeathData data) { _buff.DeactivateBuff(); }
}
