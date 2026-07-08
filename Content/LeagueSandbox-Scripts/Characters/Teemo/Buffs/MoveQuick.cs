using System;
using System.Threading;
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

public class MoveQuick : IBuffGameScript {
    private ObjAIBase _teemo;
    private Particle  _hasteParticle;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HASTE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _teemo = buff.SourceUnit;
        var spellLevel = ownerSpell.CastInfo.SpellLevel;
        if (spellLevel is < 1 or > 5) return;
        StatsModifier.MoveSpeed.PercentBonus += spellLevel switch {
            1 => 0.20f,
            2 => 0.28f,
            3 => 0.36f,
            4 => 0.44f,
            5 => 0.52f
        };

        unit.AddStatModifier(StatsModifier);
        _hasteParticle = AddParticleTarget(_teemo,        _teemo, "MoveQuick_buf2.troy", unit,
                                           buff.Duration, bone: "BUFFBONE_GLB_GROUND_LOC");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (!_teemo.HasBuff("MoveQuickPassiveDebuff")) {
            AddBuff("TeemoMoveQuickSpeed", 10000000000f, 1, ownerSpell, _teemo, _teemo, infiniteduration: true);
        }

        RemoveParticle(_hasteParticle);
    }
}