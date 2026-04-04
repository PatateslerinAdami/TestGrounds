using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class JudicatorReckoning : ISpellScript {
    private ObjAIBase      _kayle;
    private Spell          _spell;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _kayle = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ad     = _kayle.Stats.AttackDamage.Total * _spell.SpellData.Coefficient;
        var ap     = _kayle.Stats.AbilityPower.Total * _spell.SpellData.Coefficient2;
        var damage = 60f + 50f * (_spell.CastInfo.SpellLevel - 1) + ap + ad;

        AddParticleTarget(_kayle, target, spell.SpellData.HitEffectName, target, bone: spell.SpellData.HitBoneName);
        target.TakeDamage(_kayle, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
        AddBuff("JudicatorReckoning", 3,  1, _spell,  target, _kayle);
        AddBuff("HolyFervorDebuff",   5f, 1, _spell, target, _kayle);
    }
}