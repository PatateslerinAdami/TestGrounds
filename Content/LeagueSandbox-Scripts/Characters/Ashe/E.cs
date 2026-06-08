using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AsheSpiritOfTheHawk : ISpellScript {
    private ObjAIBase            _ashe;
    //private SpellMissile _hawkMissile;
    private Vector2      _targetPos;
    private Spell _spell;
    private int _goldGained = 0;
    public SpellScriptMetadata ScriptMetadata { get; }  = new () {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Arc,
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ashe       = owner;
        _spell = spell;
        
        ApiEventManager.OnKillUnit.AddListener(this, _ashe, OnKillUnit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _spell = spell;
        _targetPos = end;
        
        var range = spell.SpellData.CastRange[spell.CastInfo.SpellLevel];
        var dir = end - owner.Position;
        if (dir.LengthSquared() > 0f && dir.Length() > range) {
            _targetPos = owner.Position + Vector2.Normalize(dir) * range;
        }
        
        ScriptMetadata.MissileParameters.OverrideEndPosition = _targetPos;

        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnMissileLaunch);
    }

    private void OnMissileLaunch(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnSpellMissileEnd);
    }

    public void OnSpellCast(Spell spell) {
        if (_targetPos != _ashe.Position) {
            FaceDirection(_targetPos, _ashe);
        }
    }

    private void OnSpellMissileEnd(SpellMissile missile) {
        AddPosPerceptionBubble(_targetPos, 1000f, 5f, _ashe.Team, false);
        AddParticle(_ashe, default, "Ashe_Base_E_tar_explode",    _targetPos);
        AddParticle(_ashe, default, "Ashe_Base_E_tar_linger", _targetPos, 5f);
        ApiEventManager.OnLaunchMissile.RemoveListener(this, _spell, OnMissileLaunch);
        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnSpellMissileEnd);
        missile.SetToRemove();
    }
    
    private void OnKillUnit(DeathData data) {
        var level = _spell.CastInfo.SpellLevel;
        if (level <= 0) return;
        const int bonusGold = 3;

        
        if (_ashe is Champion champ) champ.AddGold(data.Unit, bonusGold);
        _goldGained += bonusGold;
        SetSpellToolTipVar(_ashe, 0, _goldGained, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
    }
}
