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
    private SpellMissile _hawkMissile;
    private Vector2      _targetPos;
    private Spell _spell;
    private int _goldGained = 0;
    public SpellScriptMetadata ScriptMetadata { get; }  = new () {
        TriggersSpellCasts = true,
        IsDamagingSpell = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ashe       = owner;
        _spell = spell;
        ApiEventManager.OnKillUnit.AddListener(this, _ashe, OnKillUnit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
    }

    public void OnSpellCast(Spell spell) {
        _targetPos = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
        var range = 2500f + 750f * (spell.CastInfo.SpellLevel - 1);
        var from = _ashe.Position;
        var dir = _targetPos - from;
        if (dir.LengthSquared() > 0f) {
            var distance = dir.Length();
            if (distance > range) {
                _targetPos = from + Vector2.Normalize(dir) * range;
            }
            FaceDirection(_targetPos, _ashe);
        }
        _hawkMissile = spell.CreateSpellMissile(new MissileParameters {
            Type                = MissileType.Circle,
            OverrideEndPosition = _targetPos,
            
        });
        ApiEventManager.OnSpellMissileEnd.AddListener(this, _hawkMissile, OnSpellMissileEnd);
        SpellCast(_ashe,  2, SpellSlotType.ExtraSlots, _targetPos, _targetPos, false, Vector2.Zero);
    }

    private void OnSpellMissileEnd(SpellMissile missile) {
        AddPosPerceptionBubble(_targetPos, 1000f, 5f, _ashe.Team, false);
        AddParticle(_ashe, default, "Ashe_Base_E_tar_explode",    _targetPos);
        AddParticle(_ashe, default, "Ashe_Base_E_tar_linger", _targetPos, 5f);
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

public class AsheSpiritOfTheHawkCast : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; }  = new () {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Circle
        },
        TriggersSpellCasts = false,
        IsDamagingSpell = false,
    };
}
