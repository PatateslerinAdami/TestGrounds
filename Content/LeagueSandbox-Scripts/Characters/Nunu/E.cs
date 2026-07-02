using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class IceBlast : ISpellScript {
    private ObjAIBase _nunu;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _nunu = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile) {
        var ap                = _nunu.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var dmg            = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;
        AddParticleTarget(_nunu, target, "yeti_iceBlast_tar.troy", target);
        target.TakeDamage(_nunu, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
        ApiEventManager.OnSpellHit.RemoveListener(this, spell, TargetExecute);
    }
}