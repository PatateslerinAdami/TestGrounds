using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SwainShadowGrasp : ISpellScript
{
    private ObjAIBase _swain;
    private Spell _spell;
    private bool _isActive = false;
    private PeriodicTicker _periodicTicker;
    private Vector2 _position;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _swain = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _position = end;
        _spell = spell;
        AddParticle(_swain, null, "Swain_shadowGrasp_warning_green", _position, 1f, 1f,
            enemyParticle: "Swain_shadowGrasp_warning_red");
        AddParticle(_swain, null, "swain_shadowGrasp_transform.", _position, 1f, 1f);
    }

    public void OnSpellCast(Spell spell)
    {
        _periodicTicker.Reset();
        _isActive = true;
        /*AddParticlePos(_swain, "Swain_shadowGrasp_warning", _position, _position, 1f, default);
        AddParticlePos(_swain, "swain_shadowGrasp_magic", _position, _position, 1f, default);
        AddParticlePos(_swain, "swain_shadowGrasp_cas", _position, _position, 1f, default);*/
    }

    public void OnSpellPostCast(Spell spell)
    {
    }

    public void OnUpdate(float diff)
    {
        if (!_isActive) return;
        var ticks = _periodicTicker.ConsumeTicks(diff, 875f, false, 1, 1);
        if (ticks != 1) return;
        var units = GetUnitsInRange(_swain, _position, 625f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions);

        var ap = _swain.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var dmg = 80f + 40 * (_spell.CastInfo.SpellLevel - 1) + ap;
        foreach (var unit in units)
        {
            unit.TakeDamage(_swain, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
            AddBuff("SwainShadowGraspRoot", 2f, 1, _spell, unit, _swain);
        }

        _isActive = false;
    }
}