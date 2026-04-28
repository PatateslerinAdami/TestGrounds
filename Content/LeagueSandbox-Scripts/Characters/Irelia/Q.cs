using System.Numerics;
using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class IreliaGatotsu : ISpellScript {
    private ObjAIBase      _irelia;
    private AttackableUnit _target;
    private Spell          _spell;
    private Vector2        _trueCoords;
    private bool           _isQDashPending;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _spell = spell;
        _irelia = owner;
        ApiEventManager.OnMoveSuccess.AddListener(this, owner, OnMoveSuccess);
        ApiEventManager.OnMoveFailure.AddListener(this, owner, OnMoveFailure);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        if (target == null) {
            _isQDashPending = false;
            _target         = null;
            return;
        }

        _target     = target;
        _isQDashPending = true;
        _trueCoords = target.Position - (target.Position - owner.Position).Normalized() * 80f;
        
        AddParticle(owner, null,  "irelia_gotasu_cas",     owner.Position);
        AddParticle(owner, owner, "irelia_gotasu_cas1",    owner.Position);
        AddParticle(owner, owner, "irelia_gotasu_dash_01", owner.Position);
        AddParticle(owner, owner, "irelia_gotasu_dash_02", owner.Position, 0.425f);
        PlayAnimation(owner, "Spell1", 0.5f);
        owner.DashToLocation(_trueCoords, 1400f + owner.Stats.MoveSpeed.Total, leapGravity: 0f,
                             keepFacingLastDirection: false, consideredCC: false);
    }

    private void OnMoveSuccess(AttackableUnit owner, ForceMovementParameters parameters) {
        if (!_isQDashPending || _target == null) return;
        _isQDashPending = false;
        if (_target.IsDead) return;

        var ad = owner.Stats.AttackDamage.Total;

        var damage = 20 + 20 * (_spell.CastInfo.SpellLevel - 1) + ad;

        var wasAliveBeforeHit = !_target.IsDead;

        _target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false);
        AddParticle(_target, _target, "irelia_gotasu_tar", _target.Position);
        StopAnimation(_irelia, "Spell1");
        if (!wasAliveBeforeHit || !_target.IsDead) return;
        _spell.SetCooldown(0f, true);
        const int mana = 35;
        owner.Stats.CurrentMana += mana;
        AddParticle(owner, owner, "irelia_gotasu_mana_refresh", owner.Position, bone: "BUFFBONE_GLB_GROUND_LOC");
    }

    private void OnMoveFailure(AttackableUnit owner, ForceMovementParameters parameters) {
        _isQDashPending = false;
        _target         = null;
        StopAnimation(_irelia, "Spell1");
    }
}
