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
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
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

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _pantheon = owner;
        _pantheon.StopMovement();
        FaceDirection(end, _pantheon);
        SpellCast(_pantheon, 0, SpellSlotType.ExtraSlots, end, end, false, Vector2.Zero);
    }
}

public class PantheonEChannel : ISpellScript
{
    private ObjAIBase _pantheon;
    private Particle _p;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        ChannelDuration = 0.75f,
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = false,
        CastingBreaksStealth = false,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _pantheon = owner;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell , OnLvelUpSpell);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnLvelUpSpell(Spell spell)
    {
        AddBuff("PantheonEPassive", 25000f, 1, spell, _pantheon, _pantheon, true);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var adChamp = _pantheon.Stats.AttackDamage.FlatBonus * 3.6f;
        var adNoneChamp = _pantheon.Stats.AttackDamage.FlatBonus * 1.8f;
        var dmgToChampions = 13f + 10f * (_pantheon.GetSpell("PantheonE").CastInfo.SpellLevel - 1) + adChamp;
        var dmgToNoneChampions = 6.5f + 5f * (_pantheon.GetSpell("PantheonE").CastInfo.SpellLevel - 1) + adNoneChamp;


        AddParticle(_pantheon, target, "Pantheon_Base_E_tar", target.Position, flags: FXFlags.UpdateOrientation);
        target.TakeDamage(
            _pantheon,
            target is Champion ? dmgToChampions : dmgToNoneChampions,
            DamageType.DAMAGE_TYPE_PHYSICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE,
            false
        );
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _pantheon.StopMovement();
    }

    public void OnSpellChannel(Spell spell)
    {
        AddBuff("PantheonESound", 0.75f, 1, spell, _pantheon, _pantheon);
        _p = AddParticleTarget(_pantheon, _pantheon, "Pantheon_Base_E_cas.troy", _pantheon, 0.75f,
            bone: "L_BUFFBONE_GLB_HAND_LOC", flags: FXFlags.SimulateWhileOffScreen | FXFlags.TargetDirection, followGroundTilt: false);
    }
    
    public void OnSpellChannelUpdate(Spell spell, float diff)
    {
        // Riot Pantheon_Heartseeker: 3 strikes at t=0/0.25/0.5 (immediate + 2), capped by a dedicated
        // counter (ticksRemaining=2 + the immediate cast), NOT by the cadence var. maxTicks:3 is our
        // faithful equivalent of ticksRemaining; executeImmediately:true is Riot's OnBuffActivate hit.
        ExecutePeriodically(spell.CastInfo.InstanceVars, "pantheonETick", 250f, true, maxTicks: 3, () =>
        {
            // Cone + target flags from SpellData (PantheonEChannel: 35° half, 675u, LockConeToPlayer=0
            // → direction = cast point − caster, fixed since Pantheon is rooted via StopMovement during
            // the channel). Replaces hardcoded 600u / 80° + manual flags.
            foreach (var unit in GetUnitsHitBySpell(spell))
            {
                spell.ApplyEffects(unit);
            }
        });
    }

    public void OnSpellChannelCancel(Spell spell, ChannelingStopSource reason)
    {
        RemoveParticle(_p);
    }

    public void OnSpellPostChannel(Spell spell)
    {
        RemoveParticle(_p);
    }
}