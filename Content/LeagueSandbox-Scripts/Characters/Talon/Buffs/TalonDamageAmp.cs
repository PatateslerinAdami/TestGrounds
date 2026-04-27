using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class TalonDamageAmp : IBuffGameScript {
    private ObjAIBase              _talon;
    private Spell _spell;
    private Buff           _buff;
    private AttackableUnit _unit;
    private Particle       _p1;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff  = buff;
        _talon = ownerSpell.CastInfo.Owner;
        _unit  = unit;
        _spell = ownerSpell;
        _p1    = AddParticleTarget(_talon, _unit, "talon_E_tar_dmg", _unit, buff.Duration);
        ApiEventManager.OnPreDealDamage.AddListener(this, _talon, OnPreDealDamage); 
    }

    public void OnUpdate(float diff) {
        if (_unit.IsDead) {
            _buff.DeactivateBuff();
        }
    }

    private void OnPreDealDamage(DamageData data) {
        var damageAmp = data.PostMitigationDamage;
        data.PostMitigationDamage +=  damageAmp * 0.03f + (0.03f * _spell.CastInfo.SpellLevel - 1);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { RemoveParticle(_p1);
    ApiEventManager.OnPreDealDamage.RemoveListener(this, _talon, OnPreDealDamage);
    }
}