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

internal class Gemcraft: IBuffGameScript {
    private ObjAIBase _taric;
    private Buff _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _taric = ownerSpell.CastInfo.Owner;
        _buff = buff;
        ApiEventManager.OnHitUnit.AddListener(this, _taric, OnHit);
        ApiEventManager.OnUpdateStats.AddListener(this, _taric, OnUpdateStats);
    }

    private void OnHit(DamageData data) {
        if (data.DamageResultType is DamageResultType.RESULT_DODGE or DamageResultType.RESULT_MISS) {
            return;
        }

        data.Target.TakeDamage(_taric, _taric.Stats.Armor.Total * 0.2f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, DamageResultType.RESULT_NORMAL);
        for (short i = 0; i < 4; i++) {
            _taric.Spells[i].LowerCooldown(2f);
        }
        RemoveBuff(_buff);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        SetBuffToolTipVar(_buff, 0, _taric.Stats.Armor.Total * 0.2f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}
