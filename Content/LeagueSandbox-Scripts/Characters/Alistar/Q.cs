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
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _alistar = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
    }

    public void OnSpellPostCast(Spell spell)
    {
        AddParticlePos(_alistar, "Pulverize_cas.troy", _alistar.Position, _alistar.Position, flags: FXFlags.SimulateWhileOffScreen, keywordObject: _alistar);
        var ap = _alistar.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var dmg = spell.SpellData.EffectLevelAmount[2][spell.CastInfo.SpellLevel] + ap;
        var unitsInRange = GetUnitsInRange(_alistar, _alistar.Position, 375f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var unit in unitsInRange)
        {
            AddBuff("Pulverize", 1f, 1, spell, unit, _alistar);
            unit.TakeDamage(_alistar, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
        }
    }
}