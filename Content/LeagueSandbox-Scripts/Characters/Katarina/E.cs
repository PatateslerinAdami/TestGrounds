using System.Numerics;
using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace Spells;

public class KatarinaE : ISpellScript {
    private ObjAIBase      _katarina;
    private AttackableUnit _target;
    private Vector2        _previousPos;
    private Vector2        _coords;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts             = true,
        IsDamagingSpell                = true,
        CastingBreaksStealth           = false,
        NotSingleTargetSpell           = false,
        IsDeathRecapSource             = true,
        AutoFaceDirection              = false,
        // Lets OnSpellPreCast write the landing pos into CastInfo.TargetPosition without
        // Spell.Cast clobbering it with the default 10-unit-forward stub.
        OverrideTargetPositionInScript = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _katarina = owner; }
    
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        _previousPos = _katarina.Position;
      
        const float visualBuffer = 50f;
        var overshoot = _katarina.CollisionRadius + _target.CollisionRadius + visualBuffer;
        _coords = CalcVector(overshoot, _katarina.Position, _target.Position);
        // Replay-verified: CastSpellAns.targetPosition is the landing pos, not the click
        // target's center. Sets up the wire packet that Spell.Cast will broadcast next.
        var landing3D = new Vector3(_coords.X, GetHeightAtLocation(_coords), _coords.Y);
        spell.CastInfo.TargetPosition = landing3D;
        spell.CastInfo.TargetPositionEnd = landing3D;
    }

    public void OnSpellCast(Spell spell) {
        FaceDirection(_target.Position, _katarina, true);
        PlayAnimation(_katarina, "spell3", scaleTime: 0f, scaleSpeed: 1f,
                      flags: AnimationFlags.NoBlend); // replay value also had junk bit 7 (unread client-side)

        // silent:true skips the batched _movementUpdated broadcast. NotifyTeleport always
        // fires the S4 client's Basic_Attack_Pos handler (obj_AI_Base_PImpl_Int.cpp:3848)
        // ONLY snaps the unit position when delta > 400u (= sqrt(160000)); short-range blinks
        // (auto-attack range ~125u + 180u overshoot ≈ 280u total) stay under that threshold,
        // so the client lerps from old to new position.
        var canDoSilentHandoff = IsValidTarget(_katarina, _target,
                                               SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                               SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        TeleportTo(_katarina, _coords.X, _coords.Y, silent: true);
        NotifyTeleport(_katarina, _coords);
        FaceDirection(_target.Position, _katarina, true);

        switch (_katarina.SkinID) {
            case 9: AddParticlePos(_katarina, "Katarina_Skin09_E_return", _previousPos, _previousPos); break;
            case 7:
                AddParticlePos(_katarina, "katarina_shadowStep_XMas_return", _previousPos, _previousPos);
                AddParticleTarget(_katarina, _katarina, "Katarina_XmasKatarina_beam_start_sound", _katarina);
                break;
            case 6:  AddParticlePos(_katarina, "katarina_shadowStep_Sand_return", _previousPos, _previousPos); break;
            default: AddParticlePos(_katarina, "katarina_shadowStep_return",      _previousPos, _previousPos); break;
        }

        // Enemy-only: Basic_Attack_Pos carries the post-blink position to the client and
        // InstantStop_Attack cancels any prior in-progress AA. Falls through to the
        // NotifyTeleport path above if an ally killed the target between click and now also
        // IsValidTarget rejects dead targets.
        // Replay-empirical (Kat-perspective, 79 E casts): InstantStop_Attack only fires on
        // ~27% apparently only when there's an active AA-windup to cancel. Gate the broadcast
        // on `IsAttacking` so we match Riot's wire pattern instead of always emitting.
        if (canDoSilentHandoff) {
            _katarina.RetargetAttackToWithHandoff(_target, emitInstantStop: _katarina.IsAttacking);

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

        AddBuff("KatarinaEReduction", 1.5f, 1, spell, _katarina, _katarina);
    }

    public void OnSpellPostCast(Spell spell) { }

    private static Vector2 CalcVector(in float distance, in Vector2 player, in Vector2 target) {
        return target - (player - target).Normalized() * (!IsWalkable(target.X, target.Y) ? -distance : distance);
    }
}