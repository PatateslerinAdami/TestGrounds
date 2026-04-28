using System;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TaricHammerSmash : ISpellScript {
    private ObjAIBase _taric;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _taric = owner;
    }

    public void OnSpellPostCast(Spell spell) {
        var dmg = 150f + 100f * (spell.CastInfo.SpellLevel - 1) + _taric.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        AddParticleTarget(_taric, _taric, "TaricHammerSmash_shatter", _taric);
        AddParticleTarget(_taric, _taric, "TaricHammerSmash_nova", _taric);
        var enemies = GetUnitsInRange(_taric, _taric.Position, 375f, true,
                                      SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                      SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var enemy in enemies) {
            AddParticleTarget(_taric, enemy, "Taric_GemStorm_Tar", enemy, size: 1.25f);
            enemy.TakeDamage(_taric, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                             DamageResultType.RESULT_NORMAL);
        }

        AddBuff("Radiance", 10f, 1, spell, _taric, _taric);
    }
}
