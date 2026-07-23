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
        AutoFaceDirection              = false,
        // Lets OnSpellPreCast write the landing pos into CastInfo.TargetPosition without
        // Spell.Cast clobbering it with the default 10-unit-forward stub.
        OverrideTargetPositionInScript = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _katarina = owner; }
    
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        _previousPos = _katarina.Position;
        
        _coords = GetMovePositionByCollisionOffset(_katarina, target, 50f, true);
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

        // Plain engine teleport — Riot's script side is just BBTeleportToPosition; the wire
        // (teleport-flagged 0x61 entry in the tick's ch4 movement batch, client snaps at >50u
        // per axis via MovementHelper::SetPath) is the engine's job. The old silent:true +
        // manual NotifyTeleport pair was a sandbox crutch and shipped the teleport on CHL_S2C,
        // where the client never applied it (the Yi Alpha-Strike slide bug; the old "client
        // lerps short blinks under 400u" note here was that dropped packet, misattributed).
        // Enemy-vs-ally split: attackable (enemy/neutral) blink targets get the post-blink
        // attack re-engage below; ally/ward targets get the teleport only (replay: ally-E has
        // no Basic_Attack_Pos). Checked BEFORE the mark-damage below can kill the target.
        var isAttackableTarget = IsValidTarget(_katarina, _target,
                                               SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                               SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        TeleportToPosition(_katarina, _coords.X, _coords.Y);

        switch (_katarina.SkinID) {
            case 9: AddParticlePos(_katarina, "Katarina_Skin09_E_return", _previousPos, _previousPos); break;
            case 7:
                AddParticlePos(_katarina, "katarina_shadowStep_XMas_return", _previousPos, _previousPos);
                AddParticleTarget(_katarina, _katarina, "Katarina_XmasKatarina_beam_start_sound", _katarina);
                break;
            case 6:  AddParticlePos(_katarina, "katarina_shadowStep_Sand_return", _previousPos, _previousPos); break;
            default: AddParticlePos(_katarina, "katarina_shadowStep_return",      _previousPos, _previousPos); break;
        }

        // Enemy-only: Riot's script shape (S1 Lua model) — explicit windup-cancel + attack order;
        // the wire artifacts arise organically from the pipeline: InstantStop_Attack from the
        // cancel ONLY when a windup was live (replay-empirical, Kat-perspective, 79 E casts: ISA
        // on ~27% = exactly the with-windup subset — BBCancelAutoAttack's shape), and
        // Basic_Attack_Pos with the post-blink position when the fresh attack starts (in melee
        // range post-blink = same tick, matching the replay's +0ms). Falls through to the
        // engine's batched teleport above for ally/ward targets (IsValidTarget also rejects
        // dead targets).
        if (isAttackableTarget) {
            // Deliberately CancelAutoAttackIfWindingUp, NOT the BB-shaped CancelAutoAttack(unit,
            // reset) — replay 4d3ed764 (79 E casts) shows: an in-windup E cancels the swing AND
            // refunds its attack cooldown (instant re-attack: prevAA→E→nextAA gaps of 70/238ms),
            // a between-swings E leaves the running timer untouched (next AA on schedule — E is
            // NOT an AA reset in 4.20), and the ISA rate (~27%) ≈ the windup share of the attack
            // cycle, i.e. EVERY live windup is cancelled + announced. Attribution — three
            // mechanisms RULED OUT: the instacast itself (instacasts don't cancel windups,
            // wiki-confirmed), the teleport (byte-matched Actor_Common::SetPosition, which
            // BBTeleportToPosition calls, is pure position mechanics — no attack touch), and
            // the attack order below (same-target orders early-return, target-switch cancels
            // are silent — neither matches the ISA wire). Riot's actual server-side trigger is
            // unknown (server binaries unavailable); this line reproduces the wire-proven
            // BEHAVIOR regardless of which internal path Riot used.
            _katarina.CancelAutoAttackIfWindingUp();
            IssueOrder(_katarina, OrderType.AttackTo, targetOfOrder: _target);

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

    public void OnSpellPostCast(Spell spell)
    {
        FaceDirection(_target.Position, _katarina, true);
    }
}