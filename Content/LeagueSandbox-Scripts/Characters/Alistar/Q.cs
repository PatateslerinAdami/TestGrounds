using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class Pulverize : ISpellScript {
    private ObjAIBase _alistar;

    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _alistar = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var ap = _alistar.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var dmg = spell.SpellData.EffectLevelAmount[2][spell.CastInfo.SpellLevel] + ap;
        AddBuff("Pulverize", 1f, 1, spell, target, _alistar);
        target.TakeDamage(_alistar, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);
    }

    public void OnSpellPostCast(Spell spell)
    {
        SpellEffectCreate("Pulverize_cas.troy", _alistar, null, null, _alistar.Position, _alistar.Position, keywordObject: _alistar, fowVisibilityRadius: 10f);
        var unitsInRange = GetUnitsInRange(_alistar, _alistar.Position, 375f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var unit in unitsInRange)
        {
            spell.ApplyEffects(unit);
        }
    }
}