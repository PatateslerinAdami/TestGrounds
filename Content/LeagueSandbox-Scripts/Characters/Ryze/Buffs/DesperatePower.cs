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

internal class DesperatePower : IBuffGameScript {
    private ObjAIBase _ryze;
    private Particle  _p1, _p2, _p3;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1,
        IsNonDispellable = true
    };

    public StatsModifier StatsModifier { get; } = new();


    public void OnUpdate(Buff buff, float diff) { }

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ryze = buff.SourceUnit;
        //OverrideAutoAttack(_ryze, "RyzeDesperatePowerAttack", false);
        StatsModifier.SpellVamp.FlatBonus = ownerSpell.SpellData.EffectLevelAmount[2][ownerSpell.CastInfo.SpellLevel] / 100f;
        StatsModifier.MoveSpeed.FlatBonus = ownerSpell.SpellData.EffectLevelAmount[3][ownerSpell.CastInfo.SpellLevel];
        unit.AddStatModifier(StatsModifier);
        _p3 = SpellEffectCreate("ManaLeach_tar2.troy",_ryze, unit,  unit, lifetime: buff.Duration, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveOverrideAutoAttack(_ryze);
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
    }
}