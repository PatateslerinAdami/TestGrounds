using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static GameServerCore.Content.HashFunctions;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class VarusQ : ISpellScript
{
    private ObjAIBase _varus;
    private Buff      _soundBuff;
    private Particle  _p1, _p2;
    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata
    {
        TriggersSpellCasts    = true,
        IsDamagingSpell       = true,
        AutoFaceDirection     = false,
        // ChargeDuration = 1.5s — bar-fill / wire visual time. Matches VarusQ.json
        // SpellTargeter1/2.RangeGrowthDuration = 1.5 (client charge-bar grow time).
        // Range grows over CastRangeGrowthDuration = 1.3s and stays max thereafter.
        ChargeDuration        = 1.5f,
        // ChargeMaxHoldDuration = 4s — total hold time before auto-expire. Per VarusQ spell
        // description: "After 4 seconds, piercing arrow fails but refunds half its mana cost."
        // After bar-fill at 1.5s player has 2.5s extra to fire-or-let-expire.
        ChargeMaxHoldDuration = 4f,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _varus = owner;
    }

    public void OnSpellChargeStart(Spell spell)
    {
        _soundBuff = AddBuff("VarusQ", 5f, 1, spell, _varus, _varus);
        
        _p1 = AddParticleTarget(_varus, _varus, "varusqchannel.troy", _varus, spell.GetMaxHoldDuration(), 1, "Weapon", "R_PARENTING_HAND_LOC");
        _p2 = AddParticle(_varus, _varus, "varusqchannel2", default, spell.GetMaxHoldDuration(), bone: "HEAD");
    }

    public void OnSpellChargeUpdate(Spell spell, Vector3 position, bool forceStop)
    {
        if (!forceStop)
        {
            FaceDirection(new Vector2(position.X, position.Z), _varus, false);
        }
    }

    public void OnSpellChargeCancel(Spell spell, ChannelingStopSource reason)
    {
        LetGo();

        if (reason == ChannelingStopSource.TimeCompleted)
        {
            // Charge held past ChargeMaxHoldDuration (= 4s) without firing → expire with 50%
            // mana refund per VarusQ.json spell description: "After 4 seconds, piercing arrow
            // fails but refunds half its mana cost."
            float manaCost = spell.SpellData.ManaCost[spell.CastInfo.SpellLevel];
            _varus.Stats.CurrentMana += manaCost * 0.5f;
        }
        // Real interrupts (Stunned/Silenced/Charmed/Feared/Suppressed/Taunted/Die/Casting/Move):
        // no mana refund (or different policy per design — currently nothing extra happens).
    }

    public void OnSpellChargeFire(Spell spell)
    {
        // Engine-driven: GetCurrentChargeRange() interpolates SpellData.CastRange[level] →
        // CastRangeGrowthMax[level] over CastRangeGrowthDuration[level] using elapsed charge time.
        float currentRange = spell.GetCurrentChargeRange();

        Vector2 ownerPos = _varus.Position;
        Vector2 mousePos = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);

        Vector2 direction = Vector2.Normalize(mousePos - ownerPos);
        if (float.IsNaN(direction.X) || float.IsNaN(direction.Y))
        {
            direction = new Vector2(1, 0);
        }
        Vector2 castPos = ownerPos + (direction * currentRange);

        
        FaceDirection(castPos, _varus, true);
        
        // Engine fires the wire-side fire trigger (re-emit NPC_CastSpellAns with Unknown1=true +
        // spawn client-visible MissileReplication missile via VarusQMissile sub-spell).
        // Slot-based variant (consistent with SpellCast API): VarusQMissile is at ExtraSpell1
        // in Varus.json → slot 0 of SpellSlotType.ExtraSlots.
        // MissileParameters omitted — defaults to VarusQMissile.ScriptMetadata.MissileParameters
        // (Type = MissileType.Circle, defined down in the VarusQMissile class below).
        // Missile-style (default fireWithoutCasting=true). VarusQMissile at ExtraSpell1 → slot 0.
        SpellCastCharge(spell, 0, SpellSlotType.ExtraSlots, ownerPos, castPos);

        AddParticle(_varus, _varus, "varusqexpire", default);
        
        LetGo();
    }

    private void LetGo()
    {
        _p1.SetToRemove();
        _p2.SetToRemove();
        _varus.RemoveBuff(_soundBuff);
    }
}

public class VarusQMissile : ISpellScript
{
    private ObjAIBase     _varus;
    private IEventSource  _source;
    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Circle
        },
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
        _varus = owner;
        _source = new AbilityInfo(HashString("VarusQMissile"), HashString("VarusQ"));
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        if (!spell.SpellData.IsValidTarget(_varus, target)) return;

        switch (target)
        {
            case Minion:
                AddParticleTarget(_varus, target, "VarusQHitMinion_amber.troy", target);
                AddParticleTarget(_varus, target, "VarusQHitMinion.troy", target);
                break;
            default:
                AddParticleTarget(_varus, target, "VarusQHit_amber.troy", target);
                AddParticleTarget(_varus, target, "VarusQHit.troy", target);
                break;
        }
        
        target.TakeDamage(_varus, 100f, DamageType.DAMAGE_TYPE_PHYSICAL,
            DamageSource.DAMAGE_SOURCE_ATTACK, false, _source);
    }
}
