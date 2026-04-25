using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Packets.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

public class CharScriptZed : ICharScript {
    private const float  FastRunThreshold = 500f;
    private       Minion _minion;
    private static readonly Dictionary<string, string> FastRunAnimStates = new() {
        { "idle1_base", "" },
        { "idle2_base", "" },
        { "idle3_base", "" },
        { "idle1", "" },
        { "run", "Run_haste" },
        { "run_base", "Run_haste" },
        { "attack1", "" },
        { "attack2", "" }
    };
    private static readonly Dictionary<string, string> DefaultRunAnimStates = new() {
        { "idle1_base", "" },
        { "idle2_base", "" },
        { "idle3_base", "" },
        { "idle1", "" },
        { "run", "" },
        { "run_base", "" },
        { "attack1", "" },
        { "attack2", "" }
    };

    private ObjAIBase    _zed;
    private Spell        _spell;
    private bool _isFast;

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _zed = owner;
        _spell = spell;
        ApiEventManager.OnHitUnit.AddListener(this, _zed, OnHitUnit);
        ApiEventManager.OnUnitUpdateMoveOrder.AddListener(this, _zed, OnUpdateMoveOrder);
        for (short i = 0; i < 4; i++) {
            ApiEventManager.OnSpellCast.AddListener(this, _zed.Spells[i], OnSpellsCast);
        }
        ApiEventManager.OnEmote.AddListener(this, _zed, OnEmote);
        _isFast = _zed.Stats.GetTrueMoveSpeed() > FastRunThreshold;
        _zed.SetAnimStates(_isFast ? FastRunAnimStates : DefaultRunAnimStates);
    }

    public void OnUpdate(float diff) {
        var shouldBeFast = _zed.Stats.GetTrueMoveSpeed() > FastRunThreshold;
        if (shouldBeFast == _isFast) return;

        _isFast = shouldBeFast;
        _zed.SetAnimStates(_isFast ? FastRunAnimStates : DefaultRunAnimStates);
    }

    private void OnSpellsCast(Spell spell) {
        RemoveEmoteShadows();
    }

    private bool OnUpdateMoveOrder(ObjAIBase zed, OrderType orderType) {
        if (orderType is OrderType.MoveTo or OrderType.AttackMove or OrderType.AttackTo or OrderType.Stop) {
            RemoveEmoteShadows();
        }
        return true;
    }

    private void RemoveEmoteShadows() {
        var emoteShadow = _minion;
        if (emoteShadow != null && !emoteShadow.IsDead && !emoteShadow.IsToRemove()) {
            _minion = null;
            AddBuff("ExpirationTimer", 1.5f, 1, _spell, emoteShadow, emoteShadow);
        }

        RemoveBuff(_zed, "ZedShadowTaunt");
        RemoveBuff(_zed, "ZedShadowJoke");
    }

    private void OnEmote(ObjAIBase zed, Emotions emotions) {
        RemoveEmoteShadows();

        if (emotions == Emotions.DANCE) {
            var pos       = GetPointFromUnit(_zed, 50f,  -180f);
            var facingPos = GetPointFromUnit(_zed, 100f, -180f);
            _minion = AddMinion(_zed, "ZedShadow", "ZedShadow", pos, _zed.Team, _zed.SkinID, true, false);
            FaceDirection(facingPos, _minion, true);
            PlayAnimation(_minion, "Dance_Overwrite", timeScale: 1.15f);
        }

        if (emotions == Emotions.TAUNT) {
            AddBuff("ZedShadowTaunt", 8.5f, 1, _spell, _zed, _zed);
        }
        
        if (emotions == Emotions.JOKE) {
            AddBuff("ZedShadowJoke", 3f, 1, _spell, _zed, _zed);
        }
    }

    private void OnHitUnit(DamageData data) {
        if (!IsValidTarget(_zed, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        if (data.Target.Stats.CurrentHealth < data.Target.Stats.HealthPoints.Total * 0.5f && !data.Target.HasBuff("ZedPassiveCD")) {
            
            float dmgPercent;
            switch (_zed.Stats.Level) {
                case <7:  dmgPercent = 0.06f; break;
                case <17: dmgPercent = 0.08f; break;
                case >17: dmgPercent = 0.1f; break;
                
                default: dmgPercent = 0.06f; break;
            }
            data.Target.TakeDamage(_zed, data.Target.Stats.HealthPoints.Total * dmgPercent, DamageType.DAMAGE_TYPE_MAGICAL,
                                   DamageSource.DAMAGE_SOURCE_PROC, DamageResultType.RESULT_NORMAL);
            AddBuff("ZedPassiveCD", 10f, 1, _spell, data.Target, _zed);
            AddParticleTarget(_zed, data.Target, "zed_passive_proc_tar", data.Target);
        }
    }
}
