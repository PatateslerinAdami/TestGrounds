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
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class MordekaiserCOTGDot : IBuffGameScript {
    private Buff           _buff;
    private DamageData     _data;
    private ObjAIBase      _mordekaiser;
    private Spell          _spell;
    private AttackableUnit _unit;
    private float          _timer  = 1000f;
    private float          _damage = 0f;
    private Particle       _p;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.DAMAGE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _mordekaiser = ownerSpell.CastInfo.Owner;
        _unit        = unit;
        _buff        = buff;
        _spell       = ownerSpell;
        ApiEventManager.OnDeath.AddListener(this, unit, OnTargetDeath, true);
        _p = AddParticleTarget(_spell.CastInfo.Owner, _unit, "mordekeiser_cotg_tar", _unit, buff.Duration, flags: (FXFlags) 32);

        var basePercentDamage = 0.24f + 0.05f * (ownerSpell.CastInfo.SpellLevel - 1);
        var ap                = ownerSpell.CastInfo.Owner.Stats.AbilityPower.Total / 50f *  2f * 0.01f;
        _damage            = unit.Stats.HealthPoints.Total                      * (basePercentDamage + ap);

        _data = unit.TakeDamage(_mordekaiser, _damage/2, DamageType.DAMAGE_TYPE_MAGICAL,
                                DamageSource.DAMAGE_SOURCE_SPELL, false);
        ownerSpell.CastInfo.Owner.Stats.CurrentHealth += _data.PostMitigationDamage;
    }

    private void OnTargetDeath(DeathData data) {
        AddBuff("MordekaiserCOTGRevive", 30.0f, 1, _buff.OriginSpell, data.Unit, _buff.SourceUnit);
        _buff.DeactivateBuff();
    }

    public void OnUpdate(float diff) {
        _timer -= diff;
        if (!(_timer <= 0)) return;
        _data = _unit.TakeDamage(_mordekaiser, _damage/2/10f, DamageType.DAMAGE_TYPE_MAGICAL,
                                 DamageSource.DAMAGE_SOURCE_PROC, false);
        _mordekaiser.Stats.CurrentHealth += _data.PostMitigationDamage;
        _timer = 1000f;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _timer = 1000f;
        RemoveParticle(_p);
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}