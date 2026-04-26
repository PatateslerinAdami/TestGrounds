using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class UFSlash : ISpellScript {
    private ObjAIBase _malphite;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        ApiEventManager.OnMoveSuccess.AddListener(this, owner, OnMoveEnd);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _malphite = owner;
        _spell = spell;
    }


    public void OnSpellPostCast(Spell spell) {
        var owner    = spell.CastInfo.Owner;
        var current  = new Vector2(owner.Position.X,                owner.Position.Y);
        var spellPos = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
        var dist     = Vector2.Distance(current, spellPos);

        if (dist > 1200.0f) dist = 1200.0f;

        FaceDirection(spellPos, owner, true);
        var trueCoords = GetPointFromUnit(owner, dist);
        var time       = dist / 2300f;
        PlayAnimation(owner, "Spell4");
        AddParticleTarget(owner, owner, "Malphite_Base_UnstoppableForce_cas.troy", owner, 0.5f);
        //AddParticle(owner, null, ".troy", owner.Position);
        ForceMovement(owner, null, trueCoords, 2300, 0, 0, 0);
    }

    private void OnMoveEnd(AttackableUnit owner, ForceMovementParameters parameters) {
        StopAnimation(_malphite, "Spell4");

        var units = GetUnitsInRange(_malphite, _malphite.Position, 260f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions);
        foreach (var unit in units) {
            var ap     = _malphite.Stats.AbilityPower.Total;
            var damage = 200 + 100 * (_spell.CastInfo.SpellLevel - 1) + ap;
            unit.TakeDamage(_malphite, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
            AddBuff("UnstoppableForceStun", 1.5f, 1, _spell, unit, _malphite);
        }
    }
}