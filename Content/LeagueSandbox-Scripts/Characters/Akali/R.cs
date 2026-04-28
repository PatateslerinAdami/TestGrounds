using System.Numerics;
using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AkaliShadowDance : ISpellScript {
    private const float ShadowDanceBuffDuration = 100000000f;

    private ObjAIBase      _akali;
    private AttackableUnit _target;
    private Vector2        _trueCoords;
    private Spell          _spell;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _spell = spell;
        _akali = owner;
        ApiEventManager.OnKill.AddListener(this, owner, OnKill);
        ApiEventManager.OnAssist.AddListener(this, owner, OnAssist);
        ApiEventManager.OnMoveEnd.AddListener(this, owner, OnMoveEnd);
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        SyncShadowDanceBuffStacks();
        ApiEventManager.OnUpdateStats.AddListener(this, _akali, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _spell = spell;
        RemoveOneShadowDanceBuffStack();
        _target     = target;
        _trueCoords = target.Position - (target.Position - owner.Position).Normalized() * 75f;
        AddParticle(owner, null,  "akali_shadowDance_cas",       owner.Position);
        AddParticle(owner, owner, "akali_shadowDance_mis.troy",  owner.Position);
        AddParticle(owner, owner, "akali_shadowDance_return",    owner.Position);
        AddParticle(owner, owner, "akali_shadowDance_return_02", owner.Position);
        var distance = Vector2.DistanceSquared(_akali.Position, _target.Position) > 400f ? 200f : 100f;
        if (_target.HasBuff("AkaliMota")) {
            _trueCoords *= 0.5f;
        }
        _trueCoords = CalcVector(distance, _akali.Position, _target.Position);
        owner.DashToLocation(_trueCoords, 2000f, "Spell4", 0f, false, false);
    }
 
    private void OnMoveEnd(AttackableUnit owner, ForceMovementParameters parameters) {
        var ap     = _akali.Stats.AbilityPower.Total * 0.5f;
        var damage = 100 + 75 * (_spell.CastInfo.SpellLevel - 1) + ap;
        AddParticleTarget(_akali, _target, "akali_shadowDance_tar", _target);
        _target.TakeDamage(_akali, damage, DamageType.DAMAGE_TYPE_MAGICAL,
                           DamageSource.DAMAGE_SOURCE_SPELL, false);
        _akali.SetTargetUnit(_target, true);
    }

    public void OnUpdate(float diff) {
        if (_spell == null || _akali == null) {
            return;
        }

        SyncShadowDanceBuffStacks();
    }

    private void OnLevelUpSpell(Spell spell) {
        if (spell == null || spell.CastInfo.SpellLevel != 1) {
            return;
        }

        if (spell.CurrentAmmo < 2) {
            spell.AddAmmo(2 - spell.CurrentAmmo);
        }

        SyncShadowDanceBuffStacks();
    }

    private void SyncShadowDanceBuffStacks() {
        if (_spell == null || _akali == null) {
            return;
        }

        var ammo = _spell.CastInfo.SpellLevel > 0 ? _spell.CurrentAmmo : 0;
        if (ammo < 0) {
            ammo = 0;
        }

        var currentStacks = _akali.GetBuffWithName("AkaliShadowDance")?.StackCount ?? 0;
        if (currentStacks == ammo) {
            return;
        }

        if (currentStacks < ammo) {
            for (var i = currentStacks; i < ammo; i++) {
                AddOneShadowDanceBuffStack();
            }
            return;
        }

        for (var i = ammo; i < currentStacks; i++) {
            RemoveOneShadowDanceBuffStack();
        }
    }

    private void AddOneShadowDanceBuffStack() {
        AddBuff("AkaliShadowDance", ShadowDanceBuffDuration, 1, _spell, _akali, _akali, infiniteduration: true);
    }

    private void RemoveOneShadowDanceBuffStack() {
        var buff = _akali?.GetBuffWithName("AkaliShadowDance");
        if (buff == null) {
            return;
        }
        // For STACKS_AND_RENEWS we must use RemoveBuff to keep parent/child stack state and HUD updates consistent.
        RemoveBuff(_akali, "AkaliShadowDance");
    }
    
    private static Vector2 CalcVector(in float distance, in Vector2 player, in Vector2 target) {
        return target - (player - target).Normalized() * (!IsWalkable(target.X, target.Y) ? -distance : distance);
    }

    private void OnKill(DeathData data) {
        if (data.Unit is not Champion) return;
        if (_spell == null || _spell.CastInfo.SpellLevel < 1) return;
        var ammoBefore = _spell.CurrentAmmo;
        _spell.AddAmmo();
        if (_spell.CurrentAmmo > ammoBefore) {
            AddOneShadowDanceBuffStack();
        }
    }

    private void OnAssist(ObjAIBase assistant, DeathData data) {
        if (data.Unit is not Champion) return;
        if (_spell == null || _spell.CastInfo.SpellLevel < 1) return;
        var ammoBefore = _spell.CurrentAmmo;
        _spell.AddAmmo();
        if (_spell.CurrentAmmo > ammoBefore) {
            AddOneShadowDanceBuffStack();
        }
    }

    private void OnUpdateStats(AttackableUnit target, float diff) {
        var cooldown = _spell.CastInfo.SpellLevel switch {
            1 => 30f,
            2 => 22.5f,
            3 => 15f,
            _ => 30f
        };
        SetSpellToolTipVar(_akali, 0, cooldown, SpellbookType.SPELLBOOK_CHAMPION, 3, SpellSlotType.SpellSlots);
    }
}
