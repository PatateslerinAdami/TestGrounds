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
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();


    public void OnUpdate(float diff) { }

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ryze = ownerSpell.CastInfo.Owner;
        _ryze.SetAutoAttackSpell("ryzedesperatepowerattack", false);
        StatsModifier.SpellVamp.FlatBonus = 0.15f + 0.05f * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.MoveSpeed.FlatBonus = 80f;
        unit.AddStatModifier(StatsModifier);
        _p3 = AddParticleTarget(_ryze, unit, "ManaLeach_tar2.troy", unit, buff.Duration);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ryze.ResetAutoAttackSpell();
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
    }
}