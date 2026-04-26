using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class UdyrBearStance : IBuffGameScript {
    private const float ChampionLungeDistance   = 50.0f;
    private const float ChampionLungeSpeed      = 900.0f;
    private const float ChampionLungeTravelTime = 0.1f;

    private ObjAIBase _udyr;
    private Spell     _spell;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _udyr  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _udyr.ChangeModel("Udyr");
        _udyr.SetAutoAttackSpell("UdyrBearAttack", false);
        ApiEventManager.OnPreAttack.AddListener(this, _udyr, OnPreAttack, true);
        ApiEventManager.OnHitUnit.AddListener(this, _udyr, OnHit);
    }

    private void OnPreAttack(Spell spell) {
        if (spell.CastInfo.Owner != _udyr || spell.CastInfo.Targets.Count == 0) return;

        var target = (spell.CastInfo.Targets[0] as CastTarget)?.Unit;
        if (target is not Champion) return;
        if (!IsValidTarget(_udyr, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes)) return;
        if (target.HasBuff("UdyrBearStunCheck") || _udyr.MovementParameters != null) return;

        _udyr.LungeToTarget(target, ChampionLungeSpeed, keepFacingLastDirection: false,
                            followTargetMaxDistance: ChampionLungeDistance, travelTime: ChampionLungeTravelTime,
                            consideredCc: false);
    }

    private void OnHit(DamageData data) {
        if (!IsValidTarget(_udyr, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions)) return;
        if (data.Target.HasBuff("UdyrBearStunCheck")) return;
        AddBuff("UdyrBearStunCheck", 5f, 1, _spell, data.Target, _udyr);
        AddBuff("Stun", 1f, 1, _spell, data.Target, _udyr);
        AddParticleTarget(_udyr, data.Target, "udyr_bear_slam", data.Target, 1);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _udyr.ResetAutoAttackSpell();
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}
