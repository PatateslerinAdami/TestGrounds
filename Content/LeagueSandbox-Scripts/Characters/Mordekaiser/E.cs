using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class MordekaiserSyphonOfDestruction : ISpellScript
{
    private ObjAIBase _mordekaiser;
    private Spell _spell;
    private Vector2 _direction;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        IsDamagingSpell = true,
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _mordekaiser = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _spell = spell;
        owner.Stats.CurrentHealth =
            Math.Max(1, owner.Stats.CurrentHealth - (24f + 12 * (_spell.CastInfo.SpellLevel - 1)));
    }

    public void OnSpellCast(Spell spell)
    {
        _direction = new Vector2(_mordekaiser.Direction.X, _mordekaiser.Direction.Z);
    }

    public void OnSpellPostCast(Spell spell)
    {
        var enemiesInRange = GetUnitsInCone(
            _mordekaiser,
            _mordekaiser.Position,
            _direction,
            775,
            80f,
            true,
            SpellDataFlags.AffectEnemies
            | SpellDataFlags.AffectHeroes
            | SpellDataFlags.AffectMinions
            | SpellDataFlags.AffectNeutral);

        if (enemiesInRange.Count != 0) AddBuff("MordekaiserSyphonParticle", 1f, 1, _spell, _mordekaiser, _mordekaiser);
        foreach (var unit in enemiesInRange)
        {
            var ap = _mordekaiser.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
            var dmg = 70 + 45f * (_spell.CastInfo.SpellLevel - 1) + ap;

            AddParticle(_mordekaiser, unit, "mordakaiser_siphonOfDestruction_tar", unit.Position);
            AddParticle(_mordekaiser, unit, "mordakaiser_siphonOfDestruction_tar_02", unit.Position);
            unit.TakeDamage(_mordekaiser, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                false);
        }
    }
}