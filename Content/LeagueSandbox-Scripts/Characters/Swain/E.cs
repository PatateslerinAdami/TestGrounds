using System.Numerics;
using Buffs;
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

public class SwainTorment: ISpellScript {
    private ObjAIBase      _owner;
    private Spell          _spell;
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        TriggersSpellCasts   = true,
        NotSingleTargetSpell = true,
        
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _spell = spell;
        _owner = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
        ApiEventManager.OnPreDealDamage.AddListener(this, owner, OnPreDealDamage);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        AddParticleTarget(_owner, target, "swain_torment_tar", target);
        AddBuff("SwainTorment", 4f, 1, spell, target, _owner);
    }

    private void OnPreDealDamage(DamageData data) {
        if (!data.Target.HasBuff("SwainTorment")) return;
        var damageAmp = data.PostMitigationDamage;
        data.PostMitigationDamage +=  damageAmp * _spell.SpellData.Coefficient/10f + (0.03f * _spell.CastInfo.SpellLevel - 1);
    }
    
    public void OnSpellCast(Spell spell) {
        
    }

    public void OnSpellPostCast(Spell spell) {
    }
}