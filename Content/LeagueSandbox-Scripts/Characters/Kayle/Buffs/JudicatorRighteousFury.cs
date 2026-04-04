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

internal class JudicatorRighteousFury : IBuffGameScript {
    private const string AttackSpell1 = "JudicatorRighteousFuryAttack";
    private const string AttackSpell2 = "JudicatorRighteousFuryAttack2";

    private ObjAIBase _kayle;
    private Spell     _spell;
    private Particle  _p1;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _kayle = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        //_kayle.SetAutoAttackSpells(true, AttackSpell1, AttackSpell2);
        _kayle.SetAutoAttackSpell(AttackSpell1, true);
        _p1 = AddParticleTarget(_kayle, _kayle, "RighteousFuryHalo_buf", _kayle, buff.Duration, bone: "head");
        StatsModifier.Range.FlatBonus = 400f;
        unit.AddStatModifier(StatsModifier);
        ApiEventManager.OnHitUnit.AddListener(this, _kayle, OnHit);
    }

    private void OnHit(DamageData data) {
        if (!IsValidTarget(_kayle, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions| SpellDataFlags.AffectNeutral)) return;
        var ap              = +_kayle.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var dmg = 20f + 10f * (_spell.CastInfo.SpellLevel - 1) + ap;
        data.Target.TakeDamage(_kayle, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
                               DamageResultType.RESULT_NORMAL);
        AddParticleTarget(_kayle, data.Target, "RighteousFury_nova", data.Target);

        var ad     = _kayle.Stats.AttackDamage.Total * (0.2f + 0.05f * (_spell.CastInfo.SpellLevel - 1));
        var dmgAoe = dmg + ap + ad;
        
        var enemiesInRange = GetUnitsInRange(_kayle, data.Target.Position, 150f, true,
                                             SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes|
                                             SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var enemy in enemiesInRange) {
            AddParticleTarget(_kayle, enemy, "righteousfuryattack_tar", enemy);
            enemy.TakeDamage(_kayle, dmgAoe, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                             DamageResultType.RESULT_NORMAL);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_p1);
        _kayle.ResetAutoAttackSpell();
    }
}
