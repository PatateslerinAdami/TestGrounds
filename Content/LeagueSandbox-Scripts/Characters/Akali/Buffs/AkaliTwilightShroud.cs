using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class AkaliWStealth : IBuffGameScript {
    private ObjAIBase _akali;
    private Fade      _id;
    private Particle  _p1, _p2;
    
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INVISIBILITY,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        var isFirstCast = buff.BuffVars.GetBool("isFirstCast");
        _akali                             = buff.SourceUnit;
        SetStatus(_akali, StatusFlags.RevealSpecificUnit, false);
        SetStatus(_akali, StatusFlags.Stealthed, true);
        SetStatus(_akali, StatusFlags.Ghosted,   true);
        _p1 = AddParticleTarget(buff.SourceUnit, unit, "akali_twilight_buf", unit, buff.Duration);
        if (!isFirstCast) {
            _p2 = AddParticlePos(_akali, "TEMP_AkaliPoof2", unit.Position, unit.Position, buff.Duration, enemyParticle: "TEMP_AkaliPoof");
        }
        switch (_akali.Team) {
            case TeamId.TEAM_BLUE:
                _akali.SetVisibleByTeam(TeamId.TEAM_PURPLE, false);
                break;
            case TeamId.TEAM_PURPLE:
                _akali.SetVisibleByTeam(TeamId.TEAM_BLUE, false);
                break;
        }
        _akali.SetVisibleByTeam(TeamId.TEAM_NEUTRAL, false);
        

        _id                 = PushCharacterFade(_akali, 0.2f, 0.02f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        _akali.SetStatus(StatusFlags.Stealthed, false);
        _akali.SetStatus(StatusFlags.Ghosted,   false);
        switch (_akali.Team) {
            case TeamId.TEAM_BLUE:
                _akali.SetVisibleByTeam(TeamId.TEAM_PURPLE, true);
                break;
            case TeamId.TEAM_PURPLE:
                _akali.SetVisibleByTeam(TeamId.TEAM_BLUE, true);
                break;
        }
        _akali.SetVisibleByTeam(TeamId.TEAM_NEUTRAL, true);
        _id                 = PushCharacterFade(_akali, 1, 0.02f);
    }
}
