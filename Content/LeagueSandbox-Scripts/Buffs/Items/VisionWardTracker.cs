using System.Collections.Generic;
using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
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

internal class VisionWardTracker : IBuffGameScript {
    private ObjAIBase _owner;
    private Minion    _ward;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _owner = ownerSpell.CastInfo.Owner;
    }

    public int GetWardCount() {
        return _ward == null ? 0 : 1;
    }

    public void AddWard(Minion ward) {
        _ward?.TakeDamage(_ward, 5000f, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_INTERNALRAW, DamageResultType.RESULT_NORMAL);
        _ward = ward;
        ApiEventManager.OnDeath.AddListener(this, ward, OnDeath);
    }

    private void OnDeath(DeathData data) {
        AddParticleTarget(data.Unit, data.Unit, "Ward_Vision_Death", data.Unit);
        ApiEventManager.OnDeath.RemoveListener(this, data.Unit, OnDeath);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}