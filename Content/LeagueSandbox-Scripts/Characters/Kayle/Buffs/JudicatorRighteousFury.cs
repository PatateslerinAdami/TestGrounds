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

internal class JudicatorRighteousFury : IBuffGameScript
{
    private const string AttackSpell1 = "JudicatorRighteousFuryAttack";
    private const string AttackSpell2 = "JudicatorRighteousFuryAttack2";

    private ObjAIBase _kayle;
    private Spell _spell;
    private Particle _p1;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _kayle = buff.SourceUnit;
        _spell = ownerSpell;
        switch (_kayle.SkinID)
        {
            case 6: OverrideAutoAttacks(_kayle, true, AttackSpell1, AttackSpell2); break;
            default: OverrideAutoAttack(_kayle, AttackSpell1, true); break;
        }

        _p1 = SpellEffectCreate("RighteousFuryHalo_buf.troy", _kayle, _kayle, _kayle, lifetime: buff.Duration,
            boneName: "C_Buffbone_Glb_Head_Loc", flags: FXFlags.SimulateWhileOffScreen);
        StatsModifier.Range.FlatBonus = 400f;
        unit.AddStatModifier(StatsModifier);
        ApiEventManager.OnHitUnit.AddListener(this, _kayle, OnHit);
    }

    private void OnHit(DamageData data)
    {
        if (!IsValidTarget(_kayle, data.Target,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectNeutral)) return;

        SpellEffectCreate("InterventionHeal_buf.troy", _kayle, data.Target, data.Target,
            flags: FXFlags.SimulateWhileOffScreen);


        var ap = +_kayle.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var dmg = _spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] + ap;
        data.Target.TakeDamage(_kayle, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
            DamageResultType.RESULT_NORMAL);
        var ad = _kayle.Stats.AttackDamage.Total * _spell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel];
        var dmgAoe = dmg + ap + ad;

        var enemiesInRange = GetUnitsInRange(_kayle, data.Target.Position, 300f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectBuildings |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectTurrets);
        foreach (var enemy in enemiesInRange)
        {
            enemy.TakeDamage(_kayle, dmgAoe, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_p1);
        RemoveOverrideAutoAttack(_kayle);
    }
}