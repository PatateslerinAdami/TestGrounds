using System;
using System.Numerics;
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
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class RivenFengShuiEngine : ISpellScript
{
    private ObjAIBase _owner;
    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _owner = owner; }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _owner.SetSpell("RivenIzunaBlade", 3, true);
        AddBuff("RivenFengShuiEngine", 15f, 1, spell, _owner, _owner);
        spell.SetCooldown(0.5f, true);
    }

    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}

public class RivenIzunaBlade : ISpellScript
{
    private ObjAIBase _owner;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true,
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Circle
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute, false);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell)
    {
        var fengSpell = _owner.SetSpell("RivenFengShuiEngine", 3, true);

        float[] cooldowns = { 110f, 85f, 50f };
        int idx = Math.Min(spell.CastInfo.SpellLevel - 1, 2);
        fengSpell.SetCooldown(cooldowns[idx], false);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        var spellLevel = spell.CastInfo.SpellLevel;
        float baseDmg = 80f + 40f * (spellLevel - 1);
        float maxDmg = 240f + 120f * (spellLevel - 1);
        float bonusAD = _owner.Stats.AttackDamage.FlatBonus * 0.6f;

        float missingHpPct = 1f - target.Stats.CurrentHealth / target.Stats.HealthPoints.Total;
        float multiplier = Math.Min(1f + missingHpPct * 2.667f, 3f);
        float dmg = (baseDmg + bonusAD) * multiplier;
        dmg = Math.Min(dmg, maxDmg + bonusAD);

        target.TakeDamage(_owner, dmg, DamageType.DAMAGE_TYPE_PHYSICAL,
            DamageSource.DAMAGE_SOURCE_SPELL, false, spell);
        AddParticleTarget(_owner, target, "exile_Q_tar_04.troy", target, 1f);
    }

    public void OnUpdate(float diff) { }
}
