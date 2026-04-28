using System;
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

public class TrundleDesecrateBuffs : IBuffGameScript {
    private ObjAIBase _trundle;
    private Spell     _spell;
    private Particle  _p1, _p2, _p3;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _trundle                             = ownerSpell.CastInfo.Owner;
        _spell                               = ownerSpell;
        ApiEventManager.OnReceiveHeal.AddListener(this, _trundle, OnHeal);
        StatsModifier.MoveSpeed.PercentBonus = 0.2f + 0.05f * (_spell.CastInfo.SpellLevel - 1);
        StatsModifier.AttackSpeed.PercentBonus = 0.35f + 0.15f * (_spell.CastInfo.SpellLevel - 1);
        unit.AddStatModifier(StatsModifier);
        //_p1 = AddParticleTarget(_trundle, _trundle, "Trundle_W", _trundle, buff.Duration);
        _p2 = AddParticleTarget(_trundle, _trundle, "Trundle_W_Speed_buff", _trundle, buff.Duration);
        _p3 = AddParticleTarget(_trundle, _trundle, "Trundle_W_AttackSpeed_Buff", _trundle, buff.Duration, bone: "weapon");
    }

    private void OnHeal(HealData data) {
        data.HealAmount += data.HealAmount * 0.08f + 0.03f * (_spell.CastInfo.SpellLevel - 1);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}