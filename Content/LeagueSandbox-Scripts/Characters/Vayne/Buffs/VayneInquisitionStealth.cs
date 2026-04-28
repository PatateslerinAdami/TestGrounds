using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class VayneInquisitionStealth : IBuffGameScript {
    private ObjAIBase              _vayne;
    private Buff           _buff;
    private AttackableUnit _unit;
    private Fade           _id;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INVISIBILITY,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff  = buff;
        _vayne = ownerSpell.CastInfo.Owner;
        _unit  = unit;
        SetStatus(_vayne, StatusFlags.RevealSpecificUnit, false);
        SetStatus(_vayne, StatusFlags.Stealthed, true);
        SetStatus(_vayne, StatusFlags.Ghosted,   true);
        StatsModifier.MoveSpeed.PercentBonus += 0.4f;

        _unit.AddStatModifier(StatsModifier);
        _id = PushCharacterFade(_vayne, 0.4f, 0.015f);
    }


    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        PushCharacterFade(_vayne, 1f, 0.1f);
        SetStatus(_vayne, StatusFlags.Stealthed, false);
        SetStatus(_vayne, StatusFlags.Ghosted,   false);
        
    }
}