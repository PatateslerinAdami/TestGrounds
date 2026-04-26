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

public class AkaliMota : ISpellScript {
    private ObjAIBase _akali;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _akali = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        AddParticleTarget(_akali, _akali, "akali_mark_cas", _akali);
    }

    public void OnSpellCast(Spell spell) {
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var owner  = spell.CastInfo.Owner;
        var ap     = owner.Stats.AbilityPower.Total * 0.4f;
        var damage = 35f + 25 * (spell.CastInfo.SpellLevel - 1) + ap;

        target.TakeDamage(owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false);
        AddBuff("AkaliMota", 6f, 1, spell, target, owner);
    }
}
