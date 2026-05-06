using System.Threading;
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
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class CamouflageStealth : IBuffGameScript {
    private ObjAIBase _teemo;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INVISIBILITY,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _teemo = ownerSpell.CastInfo.Owner;
        SetStatus(_teemo, StatusFlags.Stealthed, true);
        SetStatus(_teemo, StatusFlags.Ghosted,   true);
        switch (_teemo.Team) {
            case TeamId.TEAM_BLUE:
                _teemo.SetVisibleByTeam(TeamId.TEAM_PURPLE, false);
                break;
            case TeamId.TEAM_PURPLE:
                _teemo.SetVisibleByTeam(TeamId.TEAM_BLUE, false);
                break;
        }

        _teemo.SetVisibleByTeam(TeamId.TEAM_NEUTRAL, false);
        PushCharacterFade(_teemo, 0.2f, 0.2f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _teemo.SetStatus(StatusFlags.Stealthed, false);
        _teemo.SetStatus(StatusFlags.Ghosted,   false);
        switch (_teemo.Team) {
            case TeamId.TEAM_BLUE:
                _teemo.SetVisibleByTeam(TeamId.TEAM_PURPLE, true);
                break;
            case TeamId.TEAM_PURPLE:
                _teemo.SetVisibleByTeam(TeamId.TEAM_BLUE, true);
                break;
        }

        _teemo.SetVisibleByTeam(TeamId.TEAM_NEUTRAL, true);
        PushCharacterFade(_teemo, 1, 0.2f);
    }
}