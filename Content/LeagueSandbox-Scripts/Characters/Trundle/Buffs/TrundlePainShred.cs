using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class TrundlePainShred : IBuffGameScript {
    private const float TickIntervalMs = 1000f;
    private const int   DrainTickCount = 4;

    private AttackableUnit _unit;
    private ObjAIBase      _trundle;
    private Spell          _spell;
    private Particle       _p1;
    private float          _tickTimer;
    private int            _ticksApplied;
    private float          _periodicDamageTotal;
    private float          _periodicDamageApplied;
    private float          _halfArmorSteal;
    private float          _halfMagicResistSteal;
    private float          _periodicArmorBonusApplied;
    private float          _periodicMagicResistBonusApplied;
    private Queue<float>   _pendingHeals;
    private Queue<float>   _pendingArmorBonus;
    private Queue<float>   _pendingMagicResistBonus;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit    = unit;
        _spell   = ownerSpell;
        _trundle = ownerSpell.CastInfo.Owner;
        ApiEventManager.OnSpellHit.AddListener(this, _trundle.GetSpell("TrundlePainHeal"), OnSpellHitHealMissile);
        if (!buff.Variables.TryGet<float>("halfArmorSteal", out _halfArmorSteal))
            _halfArmorSteal = unit.Stats.Armor.Total * 0.2f;
        if (!buff.Variables.TryGet<float>("halfMagicResistSteal", out _halfMagicResistSteal))
            _halfMagicResistSteal = unit.Stats.MagicResist.Total * 0.2f;
        if (!buff.Variables.TryGet<float>("periodicDamageTotal", out _periodicDamageTotal))
            _periodicDamageTotal = unit.Stats.HealthPoints.Total *
                                   (0.2f + (_trundle.Stats.AbilityPower.Total / 50f) * 0.01f) * 0.5f;

        _ticksApplied = 0;
        _tickTimer = 0f;
        _periodicDamageApplied = 0f;
        _periodicArmorBonusApplied = 0f;
        _periodicMagicResistBonusApplied = 0f;
        _pendingHeals = new Queue<float>();
        _pendingArmorBonus = new Queue<float>();
        _pendingMagicResistBonus = new Queue<float>();
        
        StatsModifier.Armor.FlatBonus = -_halfArmorSteal;
        StatsModifier.MagicResist.FlatBonus = -_halfMagicResistSteal;
        unit.AddStatModifier(StatsModifier);
        
        QueueTransfer(0f, _halfArmorSteal, _halfMagicResistSteal);
        SpellCast(_trundle, 1, SpellSlotType.ExtraSlots, true, _trundle, _unit.Position);
    }

    public void OnUpdate(float diff) {
        if (_ticksApplied >= DrainTickCount) return;
        if (_unit.IsDead || _trundle.IsDead) return;

        _tickTimer += diff;
        while (_tickTimer >= TickIntervalMs && _ticksApplied < DrainTickCount) {
            _tickTimer -= TickIntervalMs;
            ApplyDrainTick();
        }
    }

    private void ApplyDrainTick() {
        _ticksApplied++;

        var tickDamage = _periodicDamageTotal / DrainTickCount;
        if (_ticksApplied == DrainTickCount) tickDamage = _periodicDamageTotal - _periodicDamageApplied;
        _periodicDamageApplied += tickDamage;
        
        _p1 = AddParticleTarget(_unit, _unit, "TrundleUltParticle", _unit);
        _unit.TakeDamage(_trundle, tickDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PERIODIC,
                         DamageResultType.RESULT_NORMAL);
        var healAmount = _unit.Stats.GetPostMitigationDamage(tickDamage, DamageType.DAMAGE_TYPE_MAGICAL, _trundle);

        var armorDelta = _halfArmorSteal / DrainTickCount;
        if (_ticksApplied == DrainTickCount) armorDelta = _halfArmorSteal - _periodicArmorBonusApplied;
        _periodicArmorBonusApplied += armorDelta;

        var magicResistDelta = _halfMagicResistSteal / DrainTickCount;
        if (_ticksApplied == DrainTickCount) magicResistDelta = _halfMagicResistSteal - _periodicMagicResistBonusApplied;
        _periodicMagicResistBonusApplied += magicResistDelta;

        QueueTransfer(healAmount, armorDelta, magicResistDelta);
        SpellCast(_trundle, 1, SpellSlotType.ExtraSlots, true, _trundle, _unit.Position);

        var progression = _ticksApplied / (float)DrainTickCount;
        var totalArmorShred = _halfArmorSteal + (_halfArmorSteal * progression);
        var totalMagicResistShred = _halfMagicResistSteal + (_halfMagicResistSteal * progression);

        _unit.RemoveStatModifier(StatsModifier);
        StatsModifier.Armor.FlatBonus = -totalArmorShred;
        StatsModifier.MagicResist.FlatBonus = -totalMagicResistShred;
        _unit.AddStatModifier(StatsModifier);
    }

    private void QueueTransfer(float healAmount, float armorDelta, float magicResistDelta) {
        _pendingHeals.Enqueue(healAmount);
        _pendingArmorBonus.Enqueue(armorDelta);
        _pendingMagicResistBonus.Enqueue(magicResistDelta);
    }

    private void OnSpellHitHealMissile(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target != _trundle) return;
        if (_pendingHeals.Count == 0 || _pendingArmorBonus.Count == 0 || _pendingMagicResistBonus.Count == 0) return;

        var healAmount = _pendingHeals.Dequeue();
        var armorDelta = _pendingArmorBonus.Dequeue();
        var magicResistDelta = _pendingMagicResistBonus.Dequeue();

        if (healAmount > 0f) _trundle.TakeHeal(_trundle, healAmount, HealType.Drain, _spell);

        var trundleBuff = _trundle.GetBuffWithName("TrundlePainBuff");
        if (trundleBuff?.BuffScript is TrundlePainBuff bonusBuff) {
            bonusBuff.ApplyBonusDelta(armorDelta, magicResistDelta);
        }
    }
}
