using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class IreliaEquilibriumStrike : ISpellScript {
    private ObjAIBase _irelia;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _irelia  = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
    }

    public void OnSpellCast(Spell spell) {
        //AddParticleTarget(spell.CastInfo.Owner, spell.CastInfo.Owner, "irelia_equilibriumStrike_cas", spell.CastInfo.Owner);
    }

    public void OnSpellPostCast(Spell spell) {
        var ap     = _irelia.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var damage = 80 * spell.CastInfo.SpellLevel + ap;

        _target.TakeDamage(_irelia, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
        if (_target.Stats.CurrentHealth / _target.Stats.HealthPoints.Total * 100 >=
            _irelia.Stats.CurrentHealth   / _irelia.Stats.HealthPoints.Total   * 100) {
            AddBuff("Stun", 1f, 1, spell, _target, _irelia);
            AddParticleTarget(_irelia, _target, "irelia_equilibriumStrike_tar_01", _target);
            AddParticleTarget(_irelia, _target, "irelia_equilibriumStrike_tar_02", _target);
        } else {
            var variables      = new BuffVariables();
            variables.Set("slowAmount", 0.6f);
            var slowDuration   = new[] { 0, 1, 1.25f, 1.5f, 1.75f, 2 }[spell.CastInfo.SpellLevel];
            AddBuff("Slow", slowDuration, 1, spell, _target, _irelia, buffVariables: variables);
        }
    }


    public void OnUpdate(float diff) { }
}