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
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace Spells;

public class AsheSpiritOfTheHawk : ISpellScript {
    private ObjAIBase            _ashe;
    private Vector2      _targetPos;
    private Spell _spell;
    public SpellScriptMetadata ScriptMetadata { get; }  = new () {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
        // Without this the engine replaces CastInfo.TargetPosition/-End with a fake point 10u in
        // front of the caster before NotifyNPC_CastSpellAns (Spell.cs fakePos block). The hawk is a
        // CLIENT-side missile (no MissileReplication on Riot's wire) with LineMissileEndsAtTargetPoint=1,
        // so it flies to CastInfo.TargetPositionEnd — the fake point gave it no real endpoint and it
        // kept flying. Riot's CastSpellAns carries TargetPos == TargetPosEnd == the range-clamped
        // click (replay 567fd333: all 17 casts within rank range, E1 clicks clamped to ~2500).
        OverrideTargetPositionInScript = true,
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Arc,
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ashe       = owner;
        _spell = spell;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        
    }

    private void OnLevelUpSpell(Spell spell)
    {
        if (spell.CastInfo.SpellLevel != 1) return;
        AddBuff("ArchersMark", 25000f, 1, spell, _ashe, _ashe, true);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _spell = spell;
        _targetPos = end;

        var range = spell.SpellData.CastRange[spell.CastInfo.SpellLevel];
        var dir = end - owner.Position;
        if (dir.LengthSquared() > 0f && dir.Length() > range) {
            _targetPos = owner.Position + Vector2.Normalize(dir) * range;
        }

        // The clamped point must reach the wire: the client flies its own hawk to
        // CastInfo.TargetPositionEnd and stops there (LineMissileEndsAtTargetPoint), and the
        // server missile does the same (GetSkillshotRange EATP path) — no OverrideEndPosition needed.
        var target3D = new Vector3(_targetPos.X, GetHeightAtLocation(_targetPos), _targetPos.Y);
        spell.CastInfo.TargetPosition = target3D;
        spell.CastInfo.TargetPositionEnd = target3D;

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
        var particleName1 = _ashe.SkinID switch
        {
            1 => "Ashe_Skin01_E_tar_linger.troy",
            2 => "Ashe_Skin02_E_tar_linger.troy",
            3 => "Ashe_Skin03_E_tar_linger.troy",
            4 => "Ashe_Skin04_E_tar_linger.troy",
            5 => "Ashe_Skin05_E_tar_linger.troy",
            6 => "Ashe_Skin06_E_tar_linger.troy",
            _ => "Ashe_Base_E_tar_linger.troy"
        };
        
        var particleName2 = _ashe.SkinID switch
        {
            1 => "Ashe_Skin01_E_tar_explode.troy",
            2 => "Ashe_Skin02_E_tar_explode.troy",
            3 => "Ashe_Skin03_E_tar_explode.troy",
            4 => "Ashe_Skin04_E_tar_explode.troy",
            5 => "Ashe_Skin05_E_tar_explode.troy",
            6 => "Ashe_Skin06_E_tar_explode.troy",
            _ => "Ashe_Base_E_tar_explode.troy"
        };
        SpellEffectCreate(particleName1, _ashe, null, null, _targetPos, lifetime: 5f, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
        SpellEffectCreate(particleName2, _ashe, null, null, _targetPos, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
        ApiEventManager.OnLaunchMissile.RemoveListener(this, _spell, OnMissileLaunch);
        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnSpellMissileEnd);
        missile.SetToRemove();
    }
    
    
}
