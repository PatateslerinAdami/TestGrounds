using System;
using System.Numerics;
using CharScripts;
using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class KatarinaE : ISpellScript {
    private ObjAIBase      _katarina;
    private AttackableUnit _target;
    private Vector2        _previousPos;
    Vector2                _coords;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts   = false,
        IsDamagingSpell      = true,
        CastingBreaksStealth = false,
        NotSingleTargetSpell    = false,
        IsDeathRecapSource   = true,
        AutoFaceDirection    = false,
        CastTime             = 0f
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _katarina = owner; }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        //blink 
        _coords = CalcVector(180.0F, _katarina.Position, _target.Position);
        //1st part
        _previousPos = _katarina.Position;
        FaceDirection(_target.Position, _katarina, true);
        PlayAnimation(_katarina, "Spell3", 0.3f, flags: AnimationFlags.Override);

        TeleportTo(_katarina, _coords.X, _coords.Y);
        FaceDirection(_target.Position, _katarina, true);
        //blink animation
        switch (_katarina.SkinID) {
            case 9: AddParticlePos(_katarina, "Katarina_Skin09_E_return", _previousPos, _previousPos); break;
            case 7:
                AddParticlePos(_katarina, "katarina_shadowStep_XMas_return", _previousPos, _previousPos);
                AddParticleTarget(_katarina, _katarina, "Katarina_XmasKatarina_beam_start_sound", _katarina);
                break;
            case 6:  AddParticlePos(_katarina, "katarina_shadowStep_Sand_return", _previousPos, _previousPos); break;
            default: AddParticlePos(_katarina, "katarina_shadowStep_return",      _previousPos, _previousPos); break;
        }

        //DMG ratios Q Mark & E
        if (IsValidTarget(_katarina, _target,
                          SpellDataFlags.AffectEnemies| SpellDataFlags.AffectHeroes |
                          SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) {
            switch (_katarina.SkinID) {
                case 9:
                    AddParticleTarget(_katarina, _target, "katarina_Skin09_shadowStep_tar", _target,
                                      bone: "head"); break;
                case 7:
                    AddParticleTarget(_katarina, _target, "Katarina_XMas_shadowStep_tar", _target, bone: "head"); break;
                default: AddParticleTarget(_katarina, _target, "katarina_shadowStep_tar", _target, bone: "head"); break;
            }

            if (_target.HasBuff("KatarinaQMark")) {
                var markApRatio = _katarina.Stats.AbilityPower.Total * 0.2f;
                var markDamage  = 15 + 15 * (_katarina.GetSpell("KatarinaQ").CastInfo.SpellLevel - 1) + markApRatio;
                _target.TakeDamage(_katarina, markDamage, DamageType.DAMAGE_TYPE_MAGICAL,
                                   DamageSource.DAMAGE_SOURCE_PROC,
                                   false);
                RemoveBuff(_target, "KatarinaQMark");
            }

            var ap  = spell.CastInfo.Owner.Stats.AbilityPower.Total * 0.25f;
            var dmg = 60 + 25 * (spell.CastInfo.SpellLevel - 1) + ap;
            _target.TakeDamage(spell.CastInfo.Owner, dmg, DamageType.DAMAGE_TYPE_MAGICAL,
                               DamageSource.DAMAGE_SOURCE_SPELL,
                               false);
        }


        switch (_katarina.SkinID) {
            case 9:  AddParticleTarget(_katarina, _katarina, "Katarina_Skin09_E_cas",   _katarina); break;
            default: AddParticleTarget(_katarina, _katarina, "katarina_shadowStep_cas", _katarina); break;
        }

        //buff dmg reduction
        AddBuff("KatarinaEReduction", 1.5f, 0, spell, _katarina, _katarina);
        if (IsValidTarget(_katarina, _target,
                          SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                          SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) {
            _katarina.SetTargetUnit(_target);
        }
    }

    public void OnSpellCast(Spell spell) {
        //if (_target.IsDead && _target is Champion && _target.Team != _katarina.Team) spell.SetCooldown(0f);
    }

    public void OnSpellPostCast(Spell spell) { }

    private static Vector2 CalcVector(in float distance, in Vector2 player, in Vector2 target) {
        return target - (player - target).Normalized() * (!IsWalkable(target.X, target.Y) ? -distance : distance);
    }
}