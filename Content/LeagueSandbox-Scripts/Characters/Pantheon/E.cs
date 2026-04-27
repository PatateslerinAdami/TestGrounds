using System;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class PantheonE : ISpellScript
{
    private ObjAIBase _pantheon;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _pantheon = owner;
    }

    public void OnPostActivate(ObjAIBase owner, Spell spell)
    {
        AddBuff("PantheonEPassive", 25000f, 1, spell, _pantheon, _pantheon, true);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _pantheon = owner;
        _pantheon.StopMovement();
        SpellCast(_pantheon, 0, SpellSlotType.ExtraSlots, end, end, false, Vector2.Zero);
    }
}

public class PantheonEChannel : ISpellScript
{
    private ObjAIBase _pantheon;
    private Particle _p;
    private const float Range = 600f;
    private const float ConeAngle = 80f;
    private Vector2 _direction;
    private float _timer = 250f;
    private bool _isActive = false;
    private int _casts = 0;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        CastingBreaksStealth = false,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true,
        ChannelDuration = 0.75f,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _pantheon = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _pantheon.StopMovement();
        var targetDirection = end - _pantheon.Position;
        _direction = targetDirection.LengthSquared() > float.Epsilon
            ? Vector2.Normalize(targetDirection)
            : new Vector2(_pantheon.Direction.X, _pantheon.Direction.Z);
    }

    public void OnSpellChannel(Spell spell)
    {
        AddBuff("PantheonESound", 0.75f, 1, spell, _pantheon, _pantheon);
        _casts = 0;
        _timer = 250f;
        _isActive = true;
        _p = AddParticleTarget(_pantheon, _pantheon, "Pantheon_Base_E_cas", _pantheon, 0.75f,
            bone: "L_BUFFBONE_GLB_HAND_LOC", flags: FXFlags.GivenDirection, direction: -_pantheon.Direction);
    }

    public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
    {
        RemoveParticle(_p);
    }

    public void OnSpellPostChannel(Spell spell)
    {
        StopAnimation(_pantheon, "Spell3");
        RemoveParticle(_p);
    }

    public void OnUpdate(float diff)
    {
        if (!_isActive) return;
        _timer -= diff;
        if (_timer >= 0 || _casts >= 3) return;
        foreach (var unit in GetUnitsInCone(
                     _pantheon,
                     _pantheon.Position,
                     _direction,
                     Range,
                     ConeAngle,
                     true,
                     SpellDataFlags.AffectEnemies
                     | SpellDataFlags.AffectHeroes
                     | SpellDataFlags.AffectMinions
                     | SpellDataFlags.AffectNeutral))
        {
            DealDamage(unit);
        }

        _casts++;
        _timer = 250f;
    }

    private void DealDamage(AttackableUnit target)
    {
        var adChamp = _pantheon.Stats.AttackDamage.FlatBonus * 3.6f;
        var adNoneChamp = _pantheon.Stats.AttackDamage.FlatBonus * 1.8f;
        var dmgToChampions = 13f + 10f * (_pantheon.GetSpell("PantheonE").CastInfo.SpellLevel - 1) + adChamp;
        var dmgToNoneChampions = 6.5f + 5f * (_pantheon.GetSpell("PantheonE").CastInfo.SpellLevel - 1) + adNoneChamp;


        AddParticle(_pantheon, target, "Pantheon_Base_E_tar", target.Position);
        target.TakeDamage(
            _pantheon,
            target is Champion ? dmgToChampions : dmgToNoneChampions,
            DamageType.DAMAGE_TYPE_PHYSICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE,
            false
        );
    }
}