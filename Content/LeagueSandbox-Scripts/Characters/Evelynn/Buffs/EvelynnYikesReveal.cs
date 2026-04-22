using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class EvelynnYikesReveal   : IBuffGameScript {
    ObjAIBase        _evelynn;
    private Particle _exclamationMark;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HASTE,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _evelynn                          =  ownerSpell.CastInfo.Owner;
        _exclamationMark = AddParticleTarget(_evelynn, _evelynn, "Evelynn-Yikes", _evelynn, size: 0.8f, lifetime: buff.Duration, bone: "C_BUFFBONE_GLB_HEAD_LOC", teamOnly: _evelynn.Team);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_exclamationMark);
    }
}