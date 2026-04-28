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

internal class TalonBleedBuff : IBuffGameScript {
    private ObjAIBase              _talon;
    private Spell          _spell;
    private PeriodicTicker _periodicTicker;
    private AttackableUnit _unit;
    private Particle       _p1, _p2;
    private Buff           _buff;
    private Region         _unitBubble;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _talon      = ownerSpell.CastInfo.Owner;
        _spell      = ownerSpell;
        _unit       = unit;
        _buff       = buff;
        _p1         = AddParticleTarget(_talon, _unit, "talon_Q_bleed_indicator", _unit, buff.Duration);
        _p2         = AddParticleTarget(_talon, _unit, "talon_Q_bleed",           _unit, buff.Duration);
        _unitBubble = AddUnitPerceptionBubble(_unit, 400, 6f, _talon.Team);
    }

    public void OnUpdate(float diff) {
        if (_unit.IsDead) { _buff.DeactivateBuff(); }

        var ticks = _periodicTicker.ConsumeTicks(diff, 1000f, false, 1, 6);
        if (ticks != 1) return;
        var dmg = 1.67f + 1.67f * (_spell.CastInfo.SpellLevel - 1) * _talon.Stats.AttackDamage.FlatBonus * 0.167f;
        _unit.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PERIODIC,
                         DamageResultType.RESULT_NORMAL);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        _unitBubble.SetToRemove();
    }
}