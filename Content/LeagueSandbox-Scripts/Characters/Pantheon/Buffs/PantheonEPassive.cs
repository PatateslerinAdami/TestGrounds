using System.Threading;
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
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class PantheonEPassive : IBuffGameScript
{
    private ObjAIBase _pantheon;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.INTERNAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _pantheon = ownerSpell.CastInfo.Owner;
        ApiEventManager.OnPreDealDamage.AddListener(this, _pantheon, OnPreDealDamage);
    }

    private void OnPreDealDamage(DamageData data)
    {
        if (data.DamageSource is not DamageSource.DAMAGE_SOURCE_ATTACK) return;
        if (!(data.Target.Stats.CurrentHealth < data.Target.Stats.HealthPoints.Total * 0.15f)) return;
        data.DamageResultType = DamageResultType.RESULT_CRITICAL;
        data.PostMitigationDamage = _pantheon.Stats.AttackDamage.Total * 1.25f;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}