using System.Collections.Generic;
using System.Numerics;
using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace Spells;

public class AlphaStrike : ISpellScript
{
    private ObjAIBase _masterYi;
    private AttackableUnit _target;
    private Spell _spell;
    // Targets the chain marked during the dash. Damage is dealt to all of them at once in
    // OnChainEnd (replay: every Alpha Strike target takes damage in the same tick, not per hop).
    private readonly List<AttackableUnit> _hitTargets = new();

    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = false,
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters()
        {
            Type = MissileType.Chained,
            BounceSpellNameEnemy = "AlphaStrikeBounce",
            BounceSelection = BounceSelection.Nearest,
            MaximumHits = 4,
            CanHitSameTargetConsecutively = false,
            CanHitCaster = false,
            CanHitSameTarget = false,
            CanHitEnemies = true,
            CanHitFriends = false,
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _masterYi = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        _spell = spell;
        _hitTargets.Clear();
        // OnSpellHit fires per bounce (collects targets); OnChainEnd fires once after the LAST bounce.
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        ApiEventManager.OnSpellChainMissileEnd.AddListener(this, spell, OnChainEnd);
    }

    public void OnSpellCast(Spell spell)
    {
        AddBuff("AlphaStriking", 3f, 1, spell, _masterYi, _masterYi);
    }

    private void OnChainEnd(Spell spell, SpellMissile missile)
    {
        // Apply all bounce damage simultaneously (replay: every target lands in the same tick),
        // and emit the hit particle on each target at the same moment.
        foreach (var target in _hitTargets)
        {
            if (target == null || target.IsDead)
            {
                continue;
            }

            var ad = _masterYi.Stats.AttackDamage.Total * spell.SpellData.Coefficient;
            var dmg =
                (IsValidTarget(_masterYi, target,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions)
                    ? spell.SpellData.EffectLevelAmount[3][spell.CastInfo.SpellLevel]
                    : spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel]) + ad;

            bool isCrit = RollCrit(_masterYi, target);
            if (isCrit) dmg *= _masterYi.Stats.CriticalDamage.Total;
            AddParticleTarget(_masterYi, target, "MasterYi_Base_Q_Tar.troy", target);
            target.TakeDamage(_masterYi, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK,
                isCrit ? DamageResultType.RESULT_CRITICAL : DamageResultType.RESULT_NORMAL);
        }
        _hitTargets.Clear();

        RemoveBuff(_masterYi, "AlphaStriking");
        if (!_target.IsDead)
        {
            SpellCast(_masterYi, 1, SpellSlotType.ExtraSlots, true, _target, _masterYi.Position);
        }

        ApiEventManager.OnSpellHit.RemoveListener(this, spell, OnSpellHit);
        ApiEventManager.OnSpellChainMissileEnd.RemoveListener(this, spell, OnChainEnd);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        // Mark only — Alpha Strike deals NO damage per hop. Damage is dealt to every marked target
        // at once in OnChainEnd (replay: spread=0ms across all bounce targets).
        if (target != null && !_hitTargets.Contains(target))
        {
            _hitTargets.Add(target);
        }
    }
}

public class AlphaStrikeBounce : ISpellScript
{
    private ObjAIBase _alistar;
    private AttackableUnit _target;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
    {
        NotSingleTargetSpell = false,
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters
        {
        },
    };
}

public class AlphaStrikeTeleport : ISpellScript
{
    private ObjAIBase _masterYi;
    private AttackableUnit _target;
    private Vector2 _previousPos;
    private Vector2 _coords;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = false,
        IsDamagingSpell = true,
        CastingBreaksStealth = false,
        NotSingleTargetSpell = false,
        // Lets OnSpellPreCast write the landing pos into CastInfo.TargetPosition without
        // Spell.Cast clobbering it with the default 10-unit-forward stub.
        OverrideTargetPositionInScript = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _masterYi = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        _previousPos = _masterYi.Position;
        
        _coords = GetMovePositionByCollisionOffset(_masterYi, _target, 50f);

        var landing3D = new Vector3(_coords.X, GetHeightAtLocation(_coords), _coords.Y);
        spell.CastInfo.TargetPosition = landing3D;
        spell.CastInfo.TargetPositionEnd = landing3D;

        FaceDirection(_target.Position, _masterYi, true);
        TeleportTo(_masterYi, _coords.X, _coords.Y, silent: true);
        NotifyTeleport(_masterYi, _coords);
        FaceDirection(_target.Position, _masterYi, true);
    }

    private static Vector2 CalcVector(in float distance, in Vector2 player, in Vector2 target)
    {
        return target - (player - target).Normalized() * (!IsWalkable(target.X, target.Y) ? -distance : distance);
    }
}