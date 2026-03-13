using System.Numerics;
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
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TrundlePain : ISpellScript {
    private ObjAIBase      _trundle;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _trundle = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        AddParticleTarget(_trundle, _trundle, "Trundle_R_Cast", _trundle);  
        
    }

    public void OnSpellPostCast(Spell spell) {
        if (_target == null || _target.IsDead) return;
        SpellCast(_trundle, 2, SpellSlotType.ExtraSlots, true, _trundle, _target.Position);
    }

    public AttackableUnit GetTarget() { return _target; }
}

public class TrundlePainHealBig : ISpellScript {
    private const float DrainDurationSeconds = 4f;
    private const float ReturnDelaySeconds   = 4f;

    private ObjAIBase      _trundle;
    private Spell          _spell;
    private AttackableUnit _target;
    private TrundlePain    _mainR;
    private float          _immediateHealAmount;

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target,
        },
        TriggersSpellCasts = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _trundle = owner;
        _spell   = spell;
        var mainSpell = owner.GetSpell("TrundlePain");
        _mainR = mainSpell.Script as TrundlePain;
        ApiEventManager.OnSpellHit.AddListener(this, _spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = _mainR?.GetTarget();
        if (_target == null || _target.IsDead) return;

        var drainPercent = 0.2f + (_trundle.Stats.AbilityPower.Total / 50f) * 0.01f;
        var totalRawDamage = _target.Stats.HealthPoints.Total * drainPercent;
        var immediateRawDamage = totalRawDamage * 0.5f;
        _immediateHealAmount = _target.Stats.GetPostMitigationDamage(immediateRawDamage, DamageType.DAMAGE_TYPE_MAGICAL, _trundle);

        var totalArmorSteal = _target.Stats.Armor.Total * 0.4f;
        var totalMagicResistSteal = _target.Stats.MagicResist.Total * 0.4f;
        var halfArmorSteal = totalArmorSteal * 0.5f;
        var halfMagicResistSteal = totalMagicResistSteal * 0.5f;

        _target.TakeDamage(_trundle, immediateRawDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                           DamageResultType.RESULT_NORMAL);

        var buffVariables = new BuffVariables();
        buffVariables.Set("halfArmorSteal", halfArmorSteal);
        buffVariables.Set("halfMagicResistSteal", halfMagicResistSteal);
        buffVariables.Set("periodicDamageTotal", totalRawDamage * 0.5f);

        var totalBuffDuration = DrainDurationSeconds + ReturnDelaySeconds;
        AddBuff("TrundlePainShred", totalBuffDuration, 1, _spell, _target, _trundle, buffVariables: buffVariables);
        AddBuff("TrundlePainBuff", totalBuffDuration, 1, _spell, _trundle, _trundle, buffVariables: buffVariables);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target != _trundle) return;
        if (_immediateHealAmount <= 0f) return;
        _trundle.TakeHeal(_trundle, _immediateHealAmount, HealType.Drain, spell);
    }
}

public class TrundlePainHeal : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target
        },
        TriggersSpellCasts = false,
    };
}
