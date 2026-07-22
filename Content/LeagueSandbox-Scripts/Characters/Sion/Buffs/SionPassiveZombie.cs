using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class SionPassiveZombie : IBuffGameScript
{
    private ObjAIBase _sion;
    private DamageData _defferedDamageData;
    private float _ticks = 0;
    private Particle _p1, _p2, _p3, _p4, _p5;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        PersistsThroughDeath = true,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _ticks = 0;
        _sion = buff.SourceUnit;
        unit.Stats.CurrentHealth = unit.Stats.HealthPoints.Total;
        // Lock attack speed to exactly 1.75 during the zombie: min AND max cap = 1.75, so the
        // attack cadence (SpellData.GetCharacterAttackDelay clamps to 1/1.75s) is fixed regardless
        // of Sion's actual AS stat. This is a per-unit override on the unit itself, NOT part of the
        // StatsModifier, so it must be cleared explicitly in OnDeactivate (see below).
        OverrideUnitAttackSpeedCap(_sion, true, 1.75f, true, 1.75f);

        StatsModifier.LifeSteal.FlatBonus = 1f;
        unit.AddStatModifier(StatsModifier);

        _sion.SetStatus(StatusFlags.Ghosted, true);

        SetCharacterVoiceOverride(buff.TargetUnit, "Berserk");
        
        ApiEventManager.OnHeal.AddListener(this, _sion, OnHeal);
        ApiEventManager.OnHitUnit.AddListener(this, _sion, OnHit);
        OverrideAutoAttacks(_sion, false, "SionBasicAttackPassive2", "SionBasicAttackPassive");
        unit.SetAnimStates(new Dictionary<string, string>
        {
            { "IDLE1", "PASSIVE_IDLE1" },
            { "IDLE1_BASE", "PASSIVE_IDLE1" },
            { "IDLE2_BASE", "PASSIVE_IDLE1" },
            { "IDLE_IN", "PASSIVE_IDLE1" },
            { "RUN", "Passive_Run_Raw" },
            { "RUN_HASTE", "PASSIVE_RUN" },
            { "LAUGH", "PASSIVE_DANCE" },
            { "DANCE", "PASSIVE_DANCE" },
            { "TAUNT", "PASSIVE_DANCE" },
            { "JOKE", "PASSIVE_DANCE" },
        });
        _p1 = SpellEffectCreate("Sion_Base_Passive_Hand.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            scale: 2.5f,
            boneName: "L_Buffbone_Glb_Hand_Loc",
            flags: FXFlags.SimulateWhileOffScreen);
        _p2 = SpellEffectCreate("Sion_Base_Passive_Hand.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            scale: 2.5f,
            boneName: "R_Buffbone_Glb_Hand_Loc",
            flags: FXFlags.SimulateWhileOffScreen);
        _p3 = SpellEffectCreate("Sion_Base_Passive_Skin.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
        _p4 = SpellEffectCreate("Sion_Base_Passive_Cas.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
        _p5 = SpellEffectCreate("Sion_Base_Passive_Smoke.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
        for (var i = 0; i < 4; i++)
        {
            SetSpell(_sion, "SionPassiveSpeed", SpellSlotType.SpellSlots, i);
        }
    }

    public void OnUpdate(Buff buff, float diff)
    {
        if (_sion.Stats.CurrentHealth <= 0)
        {
            RemoveBuff(buff);
        }
        else
        {
            ExecutePeriodically(buff.BuffVars, "SionPassiveZombieHealthDecayTicks", 250f, false, 0,
                () =>
                {
                    _ticks++;
                    var level = _sion.Stats.Level;
                    var baseLoss = 1f + level;
                    var increment = 0.7f + 0.7f * level;
                    _sion.Stats.CurrentHealth -= baseLoss + (_ticks - 1) * increment;
                });
        }
    }

    private void OnHeal(HealData data)
    {
        if (data.HealType is HealType.SelfHeal or HealType.SpellVamp or HealType.PhysicalVamp
            or HealType.HealthRegeneration or HealType.Drain)
        {
            data.HealAmount = 0f;
        }
    }

    private void OnHit(DamageData data)
    {
        var dmg = data.Target.Stats.HealthPoints.Total * 0.1f;
        if (!IsValidTarget(_sion, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes))
        {
            dmg = System.Math.Min(dmg, 75f);
        }

        data.Target.TakeDamage(_sion, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PROC,
            DamageResultType.RESULT_NORMAL);
    }


    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.OnHitUnit.RemoveListener(this, _sion, OnHit);
        _sion.SetStatus(StatusFlags.Ghosted, false);
        ResetCharacterVoiceOverride(_sion);
        _sion.RemoveOverrideAutoAttack();
        // Clear the 1.75 min/max attack-speed lock set in OnActivate. Replay-verified (Sion rlp
        // bae83ecc, t=382.597): Riot does NOT drop the override flags — it keeps DoOverrideMax/Min
        // TRUE and sends the sentinel value -1.0 (client + server GetCharacterAttackDelay treat any
        // value <= 0 as "no cap" -> gcd default). Sending 0/false instead makes the client clamp AS
        // to 0 -> attacks look frozen/super slow.
        OverrideUnitAttackSpeedCap(_sion, true, -1f, true, -1f);
        unit.SetAnimStates(new Dictionary<string, string>
        {
            { "ATTACK1", "" },
            { "IDLE1", "" },
            { "IDLE1_BASE", "" },
            { "IDLE2_BASE", "" },
            { "IDLE_IN", "" },
            { "RUN", "" },
            { "RUN_HASTE", "" },
            { "CRIT", "" },
            { "LAUGH", "" },
            { "DANCE", "" },
            { "TAUNT", "" },
            { "JOKE", "" },
        });
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
        RemoveParticle(_p4);
        RemoveParticle(_p5);
        SetSpell(_sion, "SionQ", SpellSlotType.SpellSlots, 0);
        SetSpell(_sion, "SionW", SpellSlotType.SpellSlots, 1);
        SetSpell(_sion, "SionE", SpellSlotType.SpellSlots, 2);
        SetSpell(_sion, "SionR", SpellSlotType.SpellSlots, 3);
        RemoveBuff(_sion, "SionPassiveSoundEnd", _sion);
        SealSpellSlot(_sion, SpellSlotType.SummonerSpellSlots,0, SpellbookType.SPELLBOOK_SUMMONER, false);
        SealSpellSlot(_sion, SpellSlotType.SummonerSpellSlots,1, SpellbookType.SPELLBOOK_SUMMONER, false);
        _sion.EndZombie();
    }
}