using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TormentedSoil : ISpellScript {
    private ObjAIBase _morgana;
    private Spell _spell;
    private const float TickIntervalMs = 1000f;
    private const int   MaxTickCount   = 5;
    private const float ZoneRadius     = 280f;
    private bool _isReady = false;

    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        PersistsThroughDeath = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _morgana = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        
        var missingHealthPct = 0f;
        if (target.Stats.HealthPoints.Total > 0f) {
            missingHealthPct = (target.Stats.HealthPoints.Total - target.Stats.CurrentHealth) / target.Stats.HealthPoints.Total;
        }

        missingHealthPct = missingHealthPct switch {
            < 0f => 0f,
            > 1f => 1f,
            _    => missingHealthPct
        };

        var baseMinDamage = _spell.SpellData.EffectLevelAmount[5][_spell.CastInfo.SpellLevel];
        var baseMaxDamage = _spell.SpellData.EffectLevelAmount[6][_spell.CastInfo.SpellLevel];
        var baseDamage    = baseMinDamage + (baseMaxDamage - baseMinDamage) * missingHealthPct;

        var apMinDamage = _morgana.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var apMaxDamage = _morgana.Stats.AbilityPower.Total * _spell.SpellData.Coefficient2;
        var apDamage    = apMinDamage + (apMaxDamage - apMinDamage) * missingHealthPct;

        SpellEffectCreate("FireFeet_buf.troy",_morgana, target,  target, boneName: "L_Buffbone_Glb_Foot_Loc", flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("FireFeet_buf.troy",_morgana, target,  target, boneName: "R_Buffbone_Glb_Foot_Loc", flags: FXFlags.SimulateWhileOffScreen);
        target.TakeDamage(_morgana, (baseDamage + apDamage) * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        _isReady = false;
    }

    public void OnSpellPostCast(Spell spell) {
        var owner = spell.CastInfo.Owner;
        var pos   = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
        var particleName = _morgana.SkinID switch
        {
            4 => "Morgana_Blackthorn_Puddle_Green_Tar.troy",
            5 => "Morgana_Skin05_W_Tar_Green.troy",
            6 => "Morgana_Skin06_W_Tar_Green.troy",
            _ => "Morgana_Base_W_Tar_green.troy",
        };
        var particleNameForEnemy = _morgana.SkinID switch
        {
            4 => "Morgana_Blackthorn_Puddle_Red_Tar.troy",
            5 => "Morgana_Skin05_W_Tar_Red.troy",
            6 => "Morgana_Skin06_W_Tar_Red.troy",
            _ => "Morgana_Base_W_Tar_red.troy",
        };
        SpellEffectCreate(particleName,owner, null, null, pos, pos, lifetime: 5f, effectNameForEnemy: particleNameForEnemy, flags: FXFlags.SimulateWhileOffScreen);
        _isReady = true;
    }

    public void OnUpdate(float diff)
    {
        if(!_isReady) return;
        ExecutePeriodically(_spell.CastInfo.InstanceVars, "LastTimeExecuted", TickIntervalMs, true, MaxTickCount, () =>
        {
            var unitsInRange = GetUnitsInRange(_morgana, new Vector2(_spell.CastInfo.TargetPosition.X, _spell.CastInfo.TargetPosition.Z), ZoneRadius, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
            foreach (var unit in unitsInRange) {
                _spell.ApplyEffects(unit);
            }
        });
    }
}
