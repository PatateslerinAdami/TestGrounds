using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TrundleCircle : ISpellScript {
    private const float PillarDurationSeconds = 6.0f;

    private ObjAIBase _trundle;
    private Spell     _spell;
    private Vector2   _end;
    private Minion    _pillar;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _trundle = owner;
        _spell = spell;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _end = end;
    }

    public void OnSpellCast(Spell spell) {
        
    }

    public void OnSpellPostCast(Spell spell) {
        _pillar = AddMinion(_trundle, "TrundleWall", "IcePillar", _end, _trundle.Team, _trundle.SkinID, false, false,
                            false,
                            SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                            SpellDataFlags.AffectEnemies, isVisible: true);
        if (_pillar != null)
            AddBuff("TrundleCircle", PillarDurationSeconds, 1, _spell, _pillar, _trundle);
    }
}
