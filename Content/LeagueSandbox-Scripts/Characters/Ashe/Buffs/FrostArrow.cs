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

public class FrostArrow : IBuffGameScript {
    private ObjAIBase _ashe;
    private float    _slowAmount;
    private Particle _slowFreeze;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ashe = buff.SourceUnit;
        _slowAmount = 0.15f + 0.05f * (_ashe.GetSpell("FrostShot").CastInfo.SpellLevel - 1);
        StatsModifier.MoveSpeed.PercentBonus = -_slowAmount;
        unit.AddStatModifier(StatsModifier);
        _slowFreeze = AddParticleTarget(_ashe, unit, "Global_Freeze.troy", unit, buff.Duration);
        ApplyAssistMarker(unit, _ashe, 10.0f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_slowFreeze);
    }
}
