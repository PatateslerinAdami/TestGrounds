using System.Linq;
using System.Numerics;
using System.Threading;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class SwainDecrepify : IBuffGameScript
{
    private ObjAIBase _swain;
    AttackableUnit _unit;
    private Spell _spell;
    private PeriodicTicker _periodicTicker;
    private Particle _p, _p1, _p2;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.DAMAGE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _spell = ownerSpell;
        _swain = ownerSpell.CastInfo.Owner;
        _unit = unit;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_p2);
        AddParticleTarget(unit, null, "swain_disintegrationBeam_cas_end", unit);
        if (_swain.Model == "SwainNoRaven")
        {
            _swain.ChangeModel("SwainBird");
        }

        AddBuff("SwainBeamExpirationTimer", 0.25f, 1, ownerSpell, _unit, _unit as ObjAIBase);
    }

    public void OnUpdate(float diff)
    {
        var ticks = _periodicTicker.ConsumeTicks(diff, 1000f, true, 1);
        if (ticks != 1) return;
        var target = GetUnitsInRange(_swain, _unit.Position, 625f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectNeutral)
            .OrderBy(unit => unit is Champion).ThenBy(unit => Vector2.Distance(_unit.Position, unit.Position))
            .FirstOrDefault();
        if (target != null)
        {
            FaceDirection(target.Position, _unit, true);
            RemoveParticle(_p);
            RemoveParticle(_p1);
            RemoveParticle(_p2);
            _p = AddParticleTarget(_unit, _unit, "swain_disintegrationBeam_beam", target, 1f, bone: "Bird_head",
                targetBone: "head");
            _p1 = AddParticleTarget(_unit, target, "swain_disintegrationBeam_tar", target, 1f, bone: "head");

            var variables = new BuffVariables();
            variables.Set("slowPercent", 0.2f + 0.05f * (_spell.CastInfo.SpellLevel - 1));
            AddBuff("Slow", 3f, 1, _spell, target, _swain, buffVariables: variables);
            AddBuff("SwainBeamDamage", 3f, 1, _spell, _unit, _swain);
            target.TakeDamage(
                _swain,
                30f + 17.5f * (_swain.GetSpell("SwainDecrepify").CastInfo.SpellLevel - 1) +
                _swain.Stats.AbilityPower.Total * _swain.GetSpell("SwainDecrepify").SpellData.Coefficient,
                DamageType.DAMAGE_TYPE_MAGICAL,
                DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
        }
        else
        {
            RemoveParticle(_p);
            RemoveParticle(_p1);
            _p2 = AddParticleTarget(_unit, _unit, "swain_disintegrationBeam_beam_idle", _unit, 1f, bone: "Bird_head");
        }
    }
}