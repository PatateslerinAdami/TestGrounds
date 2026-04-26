using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Buffs;

public class UdyrTigerPunchBleed : IBuffGameScript {
    private const float TickIntervalMs = 500f;

    private float          _damageTimer = 0f;
    private AttackableUnit _unit;
    private ObjAIBase      _udyr;
    private float          _damage;
    private Buff           _buff;
    private int            _remainingProcs = 4;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.DAMAGE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit  = unit;
        _udyr = ownerSpell?.CastInfo?.Owner;
        _buff  = buff;
        _damage = 30f + 50f * (ownerSpell.CastInfo.SpellLevel - 1) +
                  _udyr.Stats.AttackDamage.Total * (1.2f + 0.1f * (ownerSpell.CastInfo.SpellLevel - 1));
        ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
    }

    public void OnUpdate(float diff) {
        if (_remainingProcs <= 0) return;

        _damageTimer -= diff;
        if (_damageTimer > 0f) return;

        _unit.TakeDamage(_udyr, _damage / 4f, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLPERSIST,
                         false);
        _remainingProcs--;
        _damageTimer = TickIntervalMs;
    }

    public void OnDeath(DeathData data) { _buff?.DeactivateBuff(); }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        if (unit.IsDead || _remainingProcs <= 0) return;
        _unit.TakeDamage(_udyr, _damage / 4f * _remainingProcs, DamageType.DAMAGE_TYPE_PHYSICAL,
                         DamageSource.DAMAGE_SOURCE_SPELLPERSIST, false);
    }
}
