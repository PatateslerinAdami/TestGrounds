using System.Threading;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class FrostShot : IBuffGameScript {
    private ObjAIBase _ashe;
    private Spell _spell;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ashe  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _ashe.SetAutoAttackSpell("FrostArrow",      true);
        ApiEventManager.OnHitUnit.AddListener(this, ownerSpell.CastInfo.Owner, OnHit);
    }

    public void OnHit(DamageData data) {
        _ashe.Stats.CurrentMana -= 8f;
        if (!IsValidTarget(_ashe, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        
        AddParticleTarget(_ashe, data.Target, "Ashe_Base_Q_tar", data.Target, 2f);
        AddBuff("FrostArrow", 2f, 1, _spell, data.Target, _ashe);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ashe.ResetAutoAttackSpell();
    }
}