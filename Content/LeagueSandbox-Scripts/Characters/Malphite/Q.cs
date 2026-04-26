using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class SeismicShard : ISpellScript {
    private ObjAIBase _malphite;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        TriggersSpellCasts   = true,
        NotSingleTargetSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _malphite = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ap     = _malphite.Stats.AbilityPower.Total * 0.6f;
        var damage = 70f  + 50f + (spell.CastInfo.SpellLevel - 1);
        
        //slow
        AddBuff("SeismicShardBuff", 4f, 1, spell, target, _malphite);
        
        AddParticleTarget(_malphite, target, "Malphite_Base_SeismicShard_tar", target);
        target.TakeDamage(_malphite, damage + ap, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
    }
}