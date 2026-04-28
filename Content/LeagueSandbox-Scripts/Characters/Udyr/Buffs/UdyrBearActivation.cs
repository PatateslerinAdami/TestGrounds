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

public class UdyrBearActivation : IBuffGameScript {
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
        
        AddParticleTarget(_udyr, _udyr, "PrimalCharge",    _udyr, buff.Duration);
        AddParticleTarget(_udyr, _udyr, "BearStance",      _udyr, buff.Duration);
        AddParticleTarget(_udyr, _udyr, "Udyr_BearStance", _udyr, buff.Duration);
        
        var spellLevel = ownerSpell.CastInfo.SpellLevel;
        if (spellLevel is < 1 or > 5) return;
        StatsModifier.MoveSpeed.PercentBonus += spellLevel switch {
            1 => 0.15f,
            2 => 0.20f,
            3 => 0.25f,
            4 => 0.30f,
            5 => 0.35f,
            _ => 0
        };
        unit.SetStatus(StatusFlags.Ghosted, true);
        unit.AddStatModifier(StatsModifier);
        AddParticleTarget(ownerSpell.CastInfo.Owner, ownerSpell.CastInfo.Owner, "Global_Haste.troy", unit,
                          buff.Duration,             bone: "BUFFBONE_GLB_GROUND_LOC");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        unit.SetStatus(StatusFlags.Ghosted, false);
    }
}