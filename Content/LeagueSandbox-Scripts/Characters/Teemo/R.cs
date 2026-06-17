using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class BantamTrap : ISpellScript {
    private ObjAIBase _teemo;
    private Spell     _spell;
    private Minion    _noxiousTrap;
    private Fade      _id;
    private Vector2   _dir;
    private Vector2           _truecoords;
    private int               _bounceCounter = 1;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts   = true,
        NotSingleTargetSpell = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _teemo = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _teemo, OnUpdateStats);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff)
    {
        SetSpellToolTipVar(_teemo, 0, _spell.GetAmmoRechageTime(), SpellbookType.SPELLBOOK_CHAMPION, 3, SpellSlotType.SpellSlots);
    }

    public void OnSpellCast(Spell spell) {
        var cursor   = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
        var current  = new Vector2(_teemo.Position.X, _teemo.Position.Y);
        var distance = cursor - current;
        
        float castRange = 600 + 150 * (spell.CastInfo.SpellLevel - 1);

        if (distance.Length() > castRange)
        {
            distance = Vector2.Normalize(distance);
            _truecoords = current + distance * castRange;
        }
        else
        {
            _truecoords = cursor;
        }

        // spawning shroom :3c
        _noxiousTrap = AddMinion(_teemo, "TeemoMushroom", "Noxious Trap", _truecoords,
                                 _teemo.Team, _teemo.SkinID, ignoreCollision: true);
        
        //buff for duration tracking and killing on end duration
        _noxiousTrap.Stats.ManaPoints.BaseValue   = 600f;
        _noxiousTrap.Stats.CurrentMana            = 600f;
        _noxiousTrap.Stats.HealthPoints.BaseValue = 1f;
        _noxiousTrap.SetCollisionRadius(0.0f); //teemo didnt have the bounce mechanic originally in this patch enable this for push and comment out bounce
        _noxiousTrap.SetStatus(StatusFlags.Ghosted,   true);
        _noxiousTrap.SetStatus(StatusFlags.Stealthed, true);
        AddBuff("NoxiousTrap", 600f, 1, spell, _noxiousTrap, _teemo);
        
        //camouflage trap
        switch (_teemo.Team) {
            case TeamId.TEAM_BLUE: _noxiousTrap.SetVisibleByTeam(_teemo.Team, true); _noxiousTrap.SetVisibleByTeam(TeamId.TEAM_PURPLE, false); break;
            case TeamId.TEAM_PURPLE: _noxiousTrap.SetVisibleByTeam(_teemo.Team, true); _noxiousTrap.SetVisibleByTeam(TeamId.TEAM_BLUE, false); break;
        }
        _noxiousTrap.SetVisibleByTeam(TeamId.TEAM_NEUTRAL, false);
        _id        = PushCharacterFade(_noxiousTrap, 0.3f, 1.5f);
        
        //vision of area
        AddUnitPerceptionBubble(_noxiousTrap, _noxiousTrap.CharData.PerceptionBubbleRadius, 600f ,_teemo.Team);

        // direction of spell
        _dir = Vector2.Normalize(distance);
        
        // check if dash is needed
        TryDashNext(true);
    }
    
    private void TryDashNext(bool initialDash)
    {
        if (_noxiousTrap == null) return;
        
        Vector2 nextPos = _noxiousTrap.Position + _dir * 350f;
        
        // stop, if area is not movable like a wall for e.g.
        if (!IsWalkable(nextPos.X, nextPos.Y))
            return;
        
        if (initialDash) {
            // stop if no shrooms are in the area
            if (!GetUnitsInRange(_noxiousTrap, _truecoords, 300f, true, SpellDataFlags.AffectFriends | SpellDataFlags.NotAffectSelf)
                    .Any(unit => unit.Model == "TeemoMushroom" && unit.Team == _teemo.Team && unit != _noxiousTrap))
                return;
            
        } else {
            // stop if no shrooms are in the area
            if (!GetUnitsInRange(_noxiousTrap, _truecoords, 300f, true, SpellDataFlags.AffectFriends | SpellDataFlags.NotAffectSelf)
                    .Any(unit => unit.Model == "TeemoMushroom" && unit.Team == _teemo.Team && unit != _noxiousTrap))
                return;
        }
        
        // register callback for OnMoveEnd when the dash is finished
        ApiEventManager.OnMoveEnd.AddListener(this, _noxiousTrap, OnMoveEnd);

        _bounceCounter += 1;
        // dash/bounce to new location
        ForceMove(_noxiousTrap, nextPos, 1000f, gravity: 50f, facing: ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, lockActions: false);

        
    }
    
    private void OnMoveEnd(AttackableUnit owner, ForceMovementParameters parameters)
    {
        if (owner != _noxiousTrap) return;
        ApiEventManager.OnMoveEnd.RemoveListener(this, _noxiousTrap, OnMoveEnd);

        // Try if it is necessary to dash again
        TryDashNext(false);
    }

}