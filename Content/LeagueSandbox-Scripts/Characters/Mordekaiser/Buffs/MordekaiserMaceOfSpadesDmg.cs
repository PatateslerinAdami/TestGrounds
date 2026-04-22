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
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class MordekaiserMaceOfSpadesDmg : IBuffGameScript {
    private ObjAIBase      _mordekaiser;
    private Spell          _spell;
    private bool           _isSingleTarget = false;
    private AttackableUnit _unit;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.DAMAGE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _mordekaiser    = ownerSpell.CastInfo.Owner;
        _spell          = ownerSpell;
        _unit           = unit;
        _isSingleTarget = buff.Variables.GetBool("isSingleTarget");
        var ap  = _mordekaiser.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
        var ad  = _mordekaiser.Stats.AttackDamage.FlatBonus * ownerSpell.SpellData.Coefficient2;
        var dmg = 80f + 30f * (_spell.CastInfo.SpellLevel -1) + ad + ap;
        AddParticleTarget(_mordekaiser, _unit, "mordakaiser_maceOfSpades_tar", _unit);
        AddBuff("MordekaiserSyphonParticle", 1f, 1,_spell, _mordekaiser, _mordekaiser);
        _unit.TakeDamage(_mordekaiser, _isSingleTarget ? dmg * 1.65f : dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, _isSingleTarget ? DamageResultType.RESULT_CRITICAL : DamageResultType.RESULT_NORMAL);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}